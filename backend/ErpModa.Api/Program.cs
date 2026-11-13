using ErpModa.Api.Data;
using ErpModa.Api.Inventario.Interfaces;
using ErpModa.Api.Inventario.Services;
using ErpModa.Api.Ventas.Interfaces;
using ErpModa.Api.Ventas.Services;
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
    // Produccion: HSTS (max-age 30 dias, incluye subdominios) + preload.
    app.UseHsts();
}

// UseHttpsRedirection activo en todos los entornos: es no-op cuando no hay
// puerto HTTPS configurado (docs/seguridad/tls-por-entorno.md).
app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
