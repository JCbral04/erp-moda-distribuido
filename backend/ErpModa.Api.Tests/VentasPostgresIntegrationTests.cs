using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ErpModa.Api.Data;
using ErpModa.Api.Inventario.Models;
using ErpModa.Api.Ventas.DTOs;
using ErpModa.Api.Ventas.Exceptions;
using ErpModa.Api.Ventas.Models;
using ErpModa.Api.Ventas.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE facturas, detalle_venta, ventas, movimientos_stock, variantes, productos, contador_factura RESTART IDENTITY CASCADE;");
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

    [Fact]
    public async Task ConfirmarConcurrente_NoDuplicaStockNiFactura()
    {
        await LimpiarAsync();
        var varianteId = await CrearVarianteAsync(5);
        await using var context = CreateContext();
        var service = new VentasService(context);
        var venta = await service.CreateAsync(new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto> { new() { VarianteId = varianteId, Cantidad = 2, PrecioUnitario = 10 } }
        });

        using var ctx1 = CreateContext();
        using var ctx2 = CreateContext();
        var s1 = new VentasService(ctx1);
        var s2 = new VentasService(ctx2);

        var t1 = Task.Run(async () => { try { await s1.ConfirmarAsync(venta.Id); return "ok"; } catch (InvalidOperationException) { return nameof(InvalidOperationException); } catch (DbUpdateException) { return nameof(DbUpdateException); } });
        var t2 = Task.Run(async () => { try { await s2.ConfirmarAsync(venta.Id); return "ok"; } catch (InvalidOperationException) { return nameof(InvalidOperationException); } catch (DbUpdateException) { return nameof(DbUpdateException); } });
        var resultados = await Task.WhenAll(t1, t2);

        Assert.Contains("ok", resultados);
        await using var verify = CreateContext();
        var v = await verify.Variantes.FirstAsync(x => x.Id == varianteId);
        Assert.Equal(3, v.Stock);
        Assert.Equal(1, await verify.Facturas.CountAsync(f => f.VentaId == venta.Id));
        Assert.Equal(1, await verify.MovimientosStock.CountAsync(m => m.ReferenciaTipo == "venta" && m.ReferenciaId == venta.Id && m.Tipo == "venta"));
        var ventaFinal = await verify.Ventas.FirstAsync(x => x.Id == venta.Id);
        Assert.Equal("Confirmada", ventaFinal.EstadoStr);
    }

    [Fact]
    public async Task AnularConcurrente_RestauraUnaVez()
    {
        await LimpiarAsync();
        var varianteId = await CrearVarianteAsync(5);
        await using var context = CreateContext();
        var service = new VentasService(context);
        var venta = await service.CreateAsync(new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto> { new() { VarianteId = varianteId, Cantidad = 2, PrecioUnitario = 10 } }
        });
        await service.ConfirmarAsync(venta.Id);

        using var ctx1 = CreateContext();
        using var ctx2 = CreateContext();
        var s1 = new VentasService(ctx1);
        var s2 = new VentasService(ctx2);

        var t1 = Task.Run(async () => { try { await s1.AnularAsync(venta.Id, new AnularVentaDto { Motivo = "m1" }); return "ok"; } catch (InvalidOperationException) { return nameof(InvalidOperationException); } catch (DbUpdateException) { return nameof(DbUpdateException); } });
        var t2 = Task.Run(async () => { try { await s2.AnularAsync(venta.Id, new AnularVentaDto { Motivo = "m2" }); return "ok"; } catch (InvalidOperationException) { return nameof(InvalidOperationException); } catch (DbUpdateException) { return nameof(DbUpdateException); } });
        var resultados = await Task.WhenAll(t1, t2);

        Assert.Contains("ok", resultados);
        await using var verify = CreateContext();
        var v = await verify.Variantes.FirstAsync(x => x.Id == varianteId);
        Assert.Equal(5, v.Stock);
        Assert.Equal(1, await verify.MovimientosStock.CountAsync(m => m.ReferenciaTipo == "venta" && m.ReferenciaId == venta.Id && m.Tipo == "anulacion_venta"));
        var ventaFinal = await verify.Ventas.FirstAsync(x => x.Id == venta.Id);
        Assert.Equal("Anulada", ventaFinal.EstadoStr);
    }

    [Fact]
    public async Task NumeracionFacturas_Concurrente_EsUnica()
    {
        await LimpiarAsync();
        var varianteId = await CrearVarianteAsync(100);
        var ventas = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            await using var context = CreateContext();
            var service = new VentasService(context);
            var venta = await service.CreateAsync(new CrearVentaDto
            {
                MetodoPago = "Efectivo",
                Detalles = new List<DetalleCrearDto> { new() { VarianteId = varianteId, Cantidad = 1, PrecioUnitario = 10 } }
            });
            ventas.Add(venta.Id);
        }

        var tasks = ventas.Select(id => Task.Run(async () =>
        {
            for (var intento = 0; intento < 5; intento++)
            {
                try
                {
                    using var ctx = CreateContext();
                    var service = new VentasService(ctx);
                    await service.ConfirmarAsync(id);
                    return;
                }
                catch (DbUpdateException ex) when (EsConflictoSerializacion(ex))
                {
                    await Task.Delay(25 * (intento + 1));
                }
                catch (InvalidOperationException ex) when (EsConflictoSerializacion(ex))
                {
                    await Task.Delay(25 * (intento + 1));
                }
            }
            throw new Exception("No se pudo confirmar la venta tras reintentos por conflicto de serializacion.");
        })).ToArray();
        await Task.WhenAll(tasks);

        await using var verify = CreateContext();
        var numeros = await verify.Facturas.Select(f => f.Numero).ToListAsync();
        Assert.Equal(5, numeros.Count);
        Assert.Equal(5, numeros.Distinct().Count());
    }

    [Fact]
    public async Task Atomicidad_ConfirmacionFallida_NoCambiaEstadoNiStock()
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

        // Venta auxiliar para tener un venta_id valido en la factura colisionante.
        var ventaAux = await service.CreateAsync(new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto> { new DetalleCrearDto { VarianteId = varianteId, Cantidad = 1, PrecioUnitario = 10 } }
        });

        await using (var seed = CreateContext())
        {
            seed.ContadoresFactura.Add(new ContadorFactura { Id = 1, Valor = 0, Prefijo = "FAC-", ActualizadoEn = DateTime.UtcNow });
            seed.Facturas.Add(new Factura { VentaId = ventaAux.Id, Numero = "FAC-000001", Fecha = DateTime.UtcNow, Subtotal = 10, Impuesto = 0, Total = 10, Estado = "emitida", CreadoEn = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => service.ConfirmarAsync(venta.Id));

        await using var verify = CreateContext();
        var v = await verify.Variantes.FirstAsync(x => x.Id == varianteId);
        Assert.Equal(10, v.Stock);
        Assert.Equal(0, await verify.Facturas.CountAsync(f => f.VentaId == venta.Id));
        Assert.Equal(1, await verify.Facturas.CountAsync(f => f.VentaId == ventaAux.Id));
        Assert.Equal(0, await verify.MovimientosStock.CountAsync(m => m.ReferenciaTipo == "venta" && m.ReferenciaId == venta.Id));
        var ventaFinal = await verify.Ventas.FirstAsync(x => x.Id == venta.Id);
        Assert.Equal("Pendiente", ventaFinal.EstadoStr);
        var contador = await verify.ContadoresFactura.FindAsync(1);
        Assert.NotNull(contador);
        Assert.Equal(0, contador!.Valor);
    }

    [Fact]
    public async Task FacturaAnulada_QuedaEnEstadoAnulada()
    {
        await LimpiarAsync();
        var varianteId = await CrearVarianteAsync(10);
        await using var context = CreateContext();
        var service = new VentasService(context);
        var venta = await service.CreateAsync(new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto> { new() { VarianteId = varianteId, Cantidad = 2, PrecioUnitario = 10 } }
        });
        await service.ConfirmarAsync(venta.Id);
        await service.AnularAsync(venta.Id, new AnularVentaDto { Motivo = "test" });

        await using var verify = CreateContext();
        var factura = await verify.Facturas.FirstAsync(f => f.VentaId == venta.Id);
        Assert.Equal("anulada", factura.Estado);
    }

    [Fact]
    public async Task AuditoriaMovimientos_ConfirmarYAnular()
    {
        await LimpiarAsync();
        var varianteId = await CrearVarianteAsync(10);
        await using var context = CreateContext();
        var service = new VentasService(context);
        var venta = await service.CreateAsync(new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto> { new() { VarianteId = varianteId, Cantidad = 2, PrecioUnitario = 10 } }
        });
        await service.ConfirmarAsync(venta.Id);
        await service.AnularAsync(venta.Id, new AnularVentaDto { Motivo = "motivo auditoria" });

        await using var verify = CreateContext();
        var movs = await verify.MovimientosStock.Where(m => m.ReferenciaTipo == "venta" && m.ReferenciaId == venta.Id).OrderBy(m => m.Id).ToListAsync();
        Assert.Equal(2, movs.Count);
        Assert.Equal("venta", movs[0].Tipo);
        Assert.Equal(-2, movs[0].Cantidad);
        Assert.Equal("anulacion_venta", movs[1].Tipo);
        Assert.Equal(2, movs[1].Cantidad);
        Assert.Equal("motivo auditoria", movs[1].Motivo);
    }

    [Fact]
    public async Task Restricciones_PostgreSQL()
    {
        await LimpiarAsync();
        await using var context = CreateContext();

        // Unicidad SKU
        var producto = new Producto { Nombre = "P", Categoria = "C", PrecioBase = 10, Activo = true };
        context.Productos.Add(producto);
        await context.SaveChangesAsync();
        context.Variantes.Add(new Variante { ProductoId = producto.Id, Talla = "M", Color = "Negro", Sku = "SKU-DUP", Stock = 1 });
        await context.SaveChangesAsync();
        context.Variantes.Add(new Variante { ProductoId = producto.Id, Talla = "S", Color = "Blanco", Sku = "SKU-DUP", Stock = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        // Stock negativo
        await using var ctx2 = CreateContext();
        var varianteNeg = new Variante { ProductoId = producto.Id, Talla = "L", Color = "Rojo", Sku = "SKU-NEG", Stock = -1 };
        ctx2.Variantes.Add(varianteNeg);
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx2.SaveChangesAsync());

        // Coherencia total
        await using var ctx3 = CreateContext();
        ctx3.Ventas.Add(new Venta { Fecha = DateTime.UtcNow, Subtotal = 10, Impuesto = 0, Total = 99, MetodoPago = MetodoPago.Efectivo, Estado = EstadoVenta.Pendiente, CreadoEn = DateTime.UtcNow, ActualizadoEn = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx3.SaveChangesAsync());

        // Factura unica por venta
        await using var ctx4 = CreateContext();
        var venta = new Venta { Fecha = DateTime.UtcNow, Subtotal = 10, Impuesto = 0, Total = 10, MetodoPago = MetodoPago.Efectivo, Estado = EstadoVenta.Pendiente, CreadoEn = DateTime.UtcNow, ActualizadoEn = DateTime.UtcNow };
        ctx4.Ventas.Add(venta);
        await ctx4.SaveChangesAsync();
        ctx4.Facturas.Add(new Factura { VentaId = venta.Id, Numero = "F-1", Fecha = DateTime.UtcNow, Subtotal = 10, Impuesto = 0, Total = 10, Estado = "emitida", CreadoEn = DateTime.UtcNow });
        await ctx4.SaveChangesAsync();
        await Assert.ThrowsAsync<PostgresException>(() => ctx4.Database.ExecuteSqlRawAsync("INSERT INTO facturas (venta_id, numero, fecha, subtotal, impuesto, total, estado, creado_en) VALUES ({0}, {1}, now(), 10, 0, 10, 'emitida', now())", venta.Id, "F-2"));

        // Contador singleton
        await using var ctx5 = CreateContext();
        ctx5.ContadoresFactura.Add(new ContadorFactura { Id = 1, Valor = 1, ActualizadoEn = DateTime.UtcNow });
        await ctx5.SaveChangesAsync();
        ctx5.ContadoresFactura.Add(new ContadorFactura { Id = 2, Valor = 2, ActualizadoEn = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx5.SaveChangesAsync());

        // Integridad referencial detalle_venta
        await using var ctx6 = CreateContext();
        ctx6.DetalleVenta.Add(new DetalleVenta { VentaId = 999999, VarianteId = 1, Cantidad = 1, PrecioUnitario = 10, Subtotal = 10 });
        await Assert.ThrowsAsync<DbUpdateException>(() => ctx6.SaveChangesAsync());
    }

    private static bool EsConflictoSerializacion(Exception ex)
    {
        for (var actual = ex; actual != null; actual = actual.InnerException)
        {
            if (actual is Npgsql.PostgresException pg && (pg.SqlState == "40001" || pg.SqlState == "40P01"))
                return true;
            var sqlState = actual.GetType().GetProperty("SqlState")?.GetValue(actual) as string;
            if (sqlState is "40001" or "40P01")
                return true;
        }
        return false;
    }

}
