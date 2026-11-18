using System.Net;
using ErpModa.Api.Data;
using ErpModa.Api.Inventario.Interfaces;
using ErpModa.Api.Inventario.Services;
using ErpModa.Api.Ventas.Interfaces;
using ErpModa.Api.Ventas.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

builder.Services.AddDbContext<ErpModaDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

builder.Services.AddScoped<IVentasService, VentasService>();
builder.Services.AddScoped<IProductosService, ProductosService>();

// HSTS con opciones explicitas (issue #60): MaxAge 30 dias declarado para que
// comentario y comportamiento coincidan. Preload e IncludeSubdomains quedan
// en false a proposito (justificacion en el pipeline).
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(30);
});

// Detras de un reverse proxy: aceptar X-Forwarded-Proto/For SOLO del proxy
// de confianza del despliegue (criterio de la review del #93).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownProxies.Add(IPAddress.Parse(
        builder.Configuration["ProxyTrustedIP"] ?? "127.0.0.1"));
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ErpModaDbContext>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // HSTS (30 dias). Sin IncludeSubdomains: el ERP se sirve en un solo host;
    // habilitarlo forzaria HTTPS en subdominios futuros sin certificado y los
    // romperia. Sin Preload: es decision de dominio del despliegue y no es
    // revocable una vez enviado a la lista de preload.
    app.UseHsts();
}

// Antes de UseHttpsRedirection: corrige esquema/host segun los headers
// del proxy de confianza configurado arriba.
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }