using ErpModa.Api.Data;
using ErpModa.Api.Inventario.Interfaces;
using ErpModa.Api.Inventario.Services;
using ErpModa.Api.Proveedores.Interfaces;
using ErpModa.Api.Proveedores.Services;
using ErpModa.Api.Ventas.Interfaces;
using ErpModa.Api.Ventas.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// Configure DbContext with PostgreSQL
builder.Services.AddDbContext<ErpModaDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// Register services as scoped (DbContext is scoped)
builder.Services.AddScoped<IVentasService, VentasService>();
builder.Services.AddScoped<IProductosService, ProductosService>();
builder.Services.AddScoped<IProveedoresService, ProveedoresService>();
builder.Services.AddScoped<IOrdenesCompraService, OrdenesCompraService>();
builder.Services.AddScoped<IAuditoriaLogService, AuditoriaLogService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();

// --- Autenticación y autorización (JWT) ---
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? "erp-moda-distribuido-super-secreto-clave-de-almenos-32-caracteres";
var jwtIssuer = jwtSection["Issuer"] ?? "erp-moda-distribuido";
var jwtAudience = jwtSection["Audience"] ?? "erp-moda-distribuido-client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Administrador", policy =>
        policy.RequireRole("Administrador"));
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ErpModaDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Add health check endpoint
app.MapHealthChecks("/health");

app.Run();
