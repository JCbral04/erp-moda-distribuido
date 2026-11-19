using System.Net;
using System.Text;
using ErpModa.Api.Auth.Configuration;
using ErpModa.Api.Auth.Services;
using ErpModa.Api.Data;
using ErpModa.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using System.Threading.RateLimiting;
using ErpModa.Api.Inventario.Interfaces;
using ErpModa.Api.Inventario.Services;
using ErpModa.Api.Ventas.Interfaces;
using ErpModa.Api.Ventas.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.Configure<JsonOptions>(options =>
{
    // Evita filtrar mensajes internos de System.Text.Json en respuestas de deserializacion.
    options.AllowInputFormatterExceptionMessages = false;
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<ErpModaDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

builder.Services.AddScoped<IVentasService, VentasService>();
builder.Services.AddScoped<IProductosService, ProductosService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// JWT (issue #82): la clave nunca se versiona; se toma de configuración/entorno.
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
var claveEfimera = false;
if (string.IsNullOrWhiteSpace(jwt.Key))
{
    if (builder.Environment.IsProduction())
        throw new InvalidOperationException("Falta la configuracion Jwt:Key (variable de entorno Jwt__Key).");
    jwt.Key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    builder.Configuration["Jwt:Key"] = jwt.Key;
    claveEfimera = true;
}
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres.");
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

var claveFirma = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        opciones.MapInboundClaims = false;
        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = claveFirma,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
        opciones.Events = new JwtBearerEvents
        {
            OnTokenValidated = contexto =>
            {
                var claims = contexto.Principal?.Claims;
                if (claims?.Any(c => c.Type == System.Security.Claims.ClaimTypes.Role) != true)
                    contexto.Fail("Token sin claim de rol.");
                if (claims?.Any(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier) != true)
                    contexto.Fail("Token sin identificador de usuario.");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(opciones =>
{
    opciones.AddPolicy("Administrador", politica => politica.RequireRole("Administrador"));
    opciones.AddPolicy("Vendedor", politica => politica.RequireRole("Vendedor"));
    opciones.AddPolicy("Almacenista", politica => politica.RequireRole("Almacenista"));
    opciones.AddPolicy("VentasConsulta", politica =>
        politica.RequireRole("Administrador", "Vendedor", "Almacenista"));
    opciones.AddPolicy("VentasEscritura", politica =>
        politica.RequireRole("Administrador", "Vendedor"));
    opciones.AddPolicy("InventarioConsulta", politica =>
        politica.RequireRole("Administrador", "Vendedor", "Almacenista"));
    opciones.AddPolicy("InventarioEscritura", politica =>
        politica.RequireRole("Administrador", "Almacenista"));
});

// HSTS con opciones explicitas (issue #60)
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(30);
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownProxies.Add(IPAddress.Parse(
        builder.Configuration["ProxyTrustedIP"] ?? "127.0.0.1"));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"codigo\":\"rate_limit_exceeded\",\"mensaje\":\"Demasiadas solicitudes. Intente mas tarde.\"}",
            cancellationToken);
    };

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? Guid.NewGuid().ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue<int>("RateLimit:AuthPermitLimit", 10),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? Guid.NewGuid().ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ErpModaDbContext>();

var app = builder.Build();

if (claveEfimera)
    app.Logger.LogWarning("Jwt:Key no configurada: se genero una clave efimera de desarrollo. Configure User Secrets antes de desplegar.");

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    // En producción se rechaza si no se puede identificar al cliente de forma confiable.
    if ((app.Environment.IsProduction()) && context.Connection.RemoteIpAddress is null)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"codigo\":\"rate_limit_exceeded\",\"mensaje\":\"Cliente no identificable.\"}");
        return;
    }
    await next();
});
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
