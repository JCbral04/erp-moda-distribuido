using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ErpModa.Api.Data;
using ErpModa.Api.Inventario.Models;
using ErpModa.Api.Ventas.DTOs;
using ErpModa.Api.Ventas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace ErpModa.Api.Tests;

public class VentasPostgresIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("erp_moda_test")
            .WithUsername("erpuser")
            .WithPassword("erp_password")
            .Build();

        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        await using var context = CreateContext();
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var resourceName in assembly.GetManifestResourceNames().Where(n => n.EndsWith("schema.sql", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync();
            await context.Database.ExecuteSqlRawAsync(sql);
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private ErpModaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpModaDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        return new ErpModaDbContext(options);
    }

    private async Task<int> CrearVarianteAsync(int stock)
    {
        await using var context = CreateContext();
        var producto = new Producto { Nombre = "P", Categoria = "C", PrecioBase = 10, Activo = true };
        var variante = new Variante { Producto = producto, Talla = "M", Color = "Negro", Sku = "SKU-PG", Stock = stock };
        context.Productos.Add(producto);
        context.Variantes.Add(variante);
        await context.SaveChangesAsync();
        return variante.Id;
    }

    private async Task LimpiarAsync()
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE facturas, detalle_venta, ventas, movimientos_stock, variantes, productos RESTART IDENTITY CASCADE;");
    }

    [Fact]
    public async Task CrearVenta_PersisteEnPostgreSQL()
    {
        await LimpiarAsync();
        var varianteId = await CrearVarianteAsync(10);
        await using var context = CreateContext();
        var service = new VentasService(context);

        var venta = await service.CreateAsync(new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto> { new DetalleCrearDto { VarianteId = varianteId, Cantidad = 2, PrecioUnitario = 10 } }
        });

        Assert.True(venta.Id > 0);
        await using var verify = CreateContext();
        Assert.Equal(1, await verify.Ventas.CountAsync());
    }

    [Fact]
    public async Task ConfirmarVenta_DescuentaStockYGeneraFactura()
    {
        await LimpiarAsync();
        var varianteId = await CrearVarianteAsync(10);
        await using var context = CreateContext();
        var service = new VentasService(context);

        var venta = await service.CreateAsync(new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto> { new DetalleCrearDto { VarianteId = varianteId, Cantidad = 2, PrecioUnitario = 10 } }
        });

        await service.ConfirmarAsync(venta.Id);

        await using var verify = CreateContext();
        var v = await verify.Variantes.FirstAsync(x => x.Id == varianteId);
        Assert.Equal(8, v.Stock);
        Assert.Equal(1, await verify.Facturas.CountAsync(f => f.VentaId == venta.Id));
    }

    [Fact]
    public async Task AnularVentaConfirmada_RestauraStock()
    {
        await LimpiarAsync();
        var varianteId = await CrearVarianteAsync(10);
        await using var context = CreateContext();
        var service = new VentasService(context);

        var venta = await service.CreateAsync(new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto> { new DetalleCrearDto { VarianteId = varianteId, Cantidad = 2, PrecioUnitario = 10 } }
        });
        await service.ConfirmarAsync(venta.Id);
        await service.AnularAsync(venta.Id, new AnularVentaDto { Motivo = "test" });

        await using var verify = CreateContext();
        var v = await verify.Variantes.FirstAsync(x => x.Id == varianteId);
        Assert.Equal(10, v.Stock);
    }

    [Fact]
    public async Task AnularVentaPendiente_NoRestauraStock()
    {
        await LimpiarAsync();
        var varianteId = await CrearVarianteAsync(10);
        await using var context = CreateContext();
        var service = new VentasService(context);

        var venta = await service.CreateAsync(new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto> { new DetalleCrearDto { VarianteId = varianteId, Cantidad = 2, PrecioUnitario = 10 } }
        });
        await service.AnularAsync(venta.Id, new AnularVentaDto { Motivo = "test" });

        await using var verify = CreateContext();
        var v = await verify.Variantes.FirstAsync(x => x.Id == varianteId);
        Assert.Equal(10, v.Stock);
    }

    [Fact]
    public async Task ConfirmarDosVeces_Lanza()
    {
        await LimpiarAsync();
        var varianteId = await CrearVarianteAsync(10);
        await using var context = CreateContext();
        var service = new VentasService(context);

        var venta = await service.CreateAsync(new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto> { new DetalleCrearDto { VarianteId = varianteId, Cantidad = 2, PrecioUnitario = 10 } }
        });
        await service.ConfirmarAsync(venta.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmarAsync(venta.Id));
    }

    [Fact]
    public async Task AnularDosVeces_Lanza()
    {
        await LimpiarAsync();
        var varianteId = await CrearVarianteAsync(10);
        await using var context = CreateContext();
        var service = new VentasService(context);

        var venta = await service.CreateAsync(new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto> { new DetalleCrearDto { VarianteId = varianteId, Cantidad = 2, PrecioUnitario = 10 } }
        });
        await service.AnularAsync(venta.Id, new AnularVentaDto { Motivo = "test" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnularAsync(venta.Id, new AnularVentaDto { Motivo = "test2" }));
    }
}
