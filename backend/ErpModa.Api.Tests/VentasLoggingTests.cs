using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ErpModa.Api.Data;
using ErpModa.Api.Middleware;
using ErpModa.Api.Ventas.DTOs;
using ErpModa.Api.Ventas.Interfaces;
using ErpModa.Api.Ventas.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpModa.Api.Tests;

public class FakeLogger<T> : ILogger<T>
{
    public List<string> Entries { get; } = new();
    IDisposable ILogger.BeginScope<TState>(TState state) => NullLogger.Instance.BeginScope(state);
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add(formatter(state, exception));
}

public class VentasLoggingTests : IDisposable
{
    private readonly string _dbName;
    private readonly ErpModaDbContext _context;
    private readonly FakeLogger<VentasService> _logger;
    private readonly int _varianteId;

    public VentasLoggingTests()
    {
        _dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ErpModaDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        _context = new ErpModaDbContext(options);
        _logger = new FakeLogger<VentasService>();

        var httpContext = new DefaultHttpContext();
        httpContext.Items["CorrelationId"] = "corr-test";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "42")
        }, "test"));
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var service = new VentasService(_context, _logger, accessor);
        _service = service;

        _varianteId = CrearVarianteConStockAsync(1000).GetAwaiter().GetResult();
    }

    private readonly IVentasService _service;

    private async Task<int> CrearVarianteConStockAsync(int stock)
    {
        var producto = new ErpModa.Api.Inventario.Models.Producto
        {
            Nombre = "P", Categoria = "C", PrecioBase = 10, Activo = true
        };
        var variante = new ErpModa.Api.Inventario.Models.Variante
        {
            Producto = producto, Talla = "M", Color = "Negro", Sku = "SKU-LOG", Stock = stock
        };
        _context.Productos.Add(producto);
        _context.Variantes.Add(variante);
        await _context.SaveChangesAsync();
        return variante.Id;
    }

    [Fact]
    public async Task CrearVenta_RegistraEventoEstructurado()
    {
        var dto = new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto>
            {
                new() { VarianteId = _varianteId, Cantidad = 1, PrecioUnitario = 10 }
            }
        };
        var venta = await _service.CreateAsync(dto);
        Assert.Contains(_logger.Entries, e => e.Contains("Venta creada") && e.Contains("corr-test") && e.Contains("42"));
    }

    [Fact]
    public async Task ConfirmarVenta_RegistraEventoEstructurado()
    {
        var dto = new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto>
            {
                new() { VarianteId = _varianteId, Cantidad = 1, PrecioUnitario = 10 }
            }
        };
        var venta = await _service.CreateAsync(dto);
        await _service.ConfirmarAsync(venta.Id);
        Assert.Contains(_logger.Entries, e => e.Contains("Venta confirmada") && e.Contains("corr-test") && e.Contains("42"));
    }

    [Fact]
    public async Task AnularVenta_RegistraEventoEstructurado()
    {
        var dto = new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto>
            {
                new() { VarianteId = _varianteId, Cantidad = 1, PrecioUnitario = 10 }
            }
        };
        var venta = await _service.CreateAsync(dto);
        await _service.AnularAsync(venta.Id, new AnularVentaDto { Motivo = "test" });
        Assert.Contains(_logger.Entries, e => e.Contains("Venta anulada") && e.Contains("corr-test") && e.Contains("42"));
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
