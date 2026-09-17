using ErpModa.Api.Data;
using ErpModa.Api.Inventario.Models;
using ErpModa.Api.Ventas.DTOs;
using ErpModa.Api.Ventas.Exceptions;
using ErpModa.Api.Ventas.Interfaces;
using ErpModa.Api.Ventas.Services;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Tests;

public class VentasServiceTests : IDisposable
{
    private readonly string _dbName;
    private readonly ErpModaDbContext _context;
    private readonly IVentasService _service;
    private readonly int _varianteId;

    public VentasServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ErpModaDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new ErpModaDbContext(options);
        _service = new VentasService(_context);

        // Variante por defecto con stock de sobra para los tests que confirman ventas.
        _varianteId = CrearVarianteConStockAsync(1000).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<int> CrearVarianteConStockAsync(int stock)
    {
        var producto = new Producto
        {
            Nombre = "Camiseta básica",
            Categoria = "Camisetas",
            PrecioBase = 10000m,
            Activo = true,
            CreadoEn = DateTime.UtcNow,
            ActualizadoEn = DateTime.UtcNow
        };
        var variante = new Variante
        {
            Producto = producto,
            Talla = "M",
            Color = "Negro",
            Sku = $"SKU-{Guid.NewGuid():N}"[..14],
            Stock = stock,
            StockMinimo = 5,
            CreadoEn = DateTime.UtcNow,
            ActualizadoEn = DateTime.UtcNow
        };
        _context.Productos.Add(producto);
        _context.Variantes.Add(variante);
        await _context.SaveChangesAsync();
        return variante.Id;
    }

    private CrearVentaDto VentaValida(string? metodoPago = "Efectivo") => new()
    {
        MetodoPago = metodoPago,
        Detalles = new List<DetalleCrearDto>
        {
            new() { VarianteId = _varianteId, Cantidad = 2, PrecioUnitario = 25000m }
        }
    };

    private CrearVentaDto VentaValida(int varianteId, int cantidad, decimal precioUnitario, string? metodoPago = "Efectivo") => new()
    {
        MetodoPago = metodoPago,
        Detalles = new List<DetalleCrearDto>
        {
            new() { VarianteId = varianteId, Cantidad = cantidad, PrecioUnitario = precioUnitario }
        }
    };

    [Fact]
    public async Task CrearVenta_ConDetallesValidos_CreaVentaPendiente()
    {
        var result = await _service.CreateAsync(VentaValida());

        Assert.True(result.Id > 0);
        Assert.Equal("Pendiente", result.Estado);
        Assert.Equal("Efectivo", result.MetodoPago);
        Assert.Equal(50000m, result.Total);
        Assert.Single(result.Detalles);
        Assert.Equal(50000m, result.Detalles[0].Subtotal);
    }

    [Fact]
    public async Task ObtenerVenta_PorId_DevuelveVentaCreada()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida());

        var result = await service.GetByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("Pendiente", result.Estado);
    }

    [Fact]
    public async Task CrearVenta_ConListaDeDetallesVacia_LanzaArgumentException()
    {
        var service = _service;
        var dto = new CrearVentaDto { MetodoPago = "Efectivo", Detalles = new List<DetalleCrearDto>() };

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CrearVenta_CantidadInvalida_LanzaArgumentException(int cantidad)
    {
        var service = _service;
        var dto = VentaValida();
        dto.Detalles[0].Cantidad = cantidad;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task CrearVenta_PrecioInvalido_LanzaArgumentException(decimal precio)
    {
        var service = _service;
        var dto = VentaValida();
        dto.Detalles[0].PrecioUnitario = precio;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
    }

    [Theory]
    [InlineData("cheque")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(null)]
    public async Task CrearVenta_MetodoPagoInvalido_LanzaArgumentException(string? metodoPago)
    {
        var service = _service;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(VentaValida(metodoPago)));
    }

    [Fact]
    public async Task ConfirmarVenta_EnPendiente_CambiaAConfirmada()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida());

        var result = await service.ConfirmarAsync(created.Id);

        Assert.Equal("Confirmada", result.Estado);
    }

    [Fact]
    public async Task ConfirmarVenta_Inexistente_LanzaKeyNotFoundException()
    {
        var service = _service;

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ConfirmarAsync(999));
    }

    [Fact]
    public async Task ConfirmarVenta_YaConfirmada_LanzaInvalidOperationException()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida());
        await service.ConfirmarAsync(created.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmarAsync(created.Id));
    }

    [Fact]
    public async Task AnularVenta_CambiaAAnulada_ConservaMotivoYNoElimina()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida());

        var result = await service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "Error de registro" });

        Assert.Equal("Anulada", result.Estado);
        Assert.Equal("Error de registro", result.MotivoAnulacion);

        var obtenida = await service.GetByIdAsync(created.Id);
        Assert.NotNull(obtenida);
        Assert.Equal("Anulada", obtenida.Estado);
    }

    [Fact]
    public async Task AnularVenta_Inexistente_LanzaKeyNotFoundException()
    {
        var service = _service;

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.AnularAsync(999, new AnularVentaDto { Motivo = "prueba" }));
    }

    [Fact]
    public async Task AnularVenta_YaAnulada_LanzaInvalidOperationException()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida());
        await service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "primer motivo" });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "segundo motivo" }));
    }

    [Fact]
    public async Task AnularVenta_SinMotivo_LanzaArgumentException()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "  " }));
    }

    [Fact]
    public async Task CrearVenta_ConVariosDetalles_CalculaSubtotalesYTotal()
    {
        var service = _service;
        var dto = new CrearVentaDto
        {
            MetodoPago = "Tarjeta",
            Detalles = new List<DetalleCrearDto>
            {
                new() { VarianteId = 1, Cantidad = 2, PrecioUnitario = 25000m },
                new() { VarianteId = 2, Cantidad = 3, PrecioUnitario = 10000m }
            }
        };

        var result = await service.CreateAsync(dto);

        Assert.Equal(2, result.Detalles.Count);
        Assert.Equal(50000m, result.Detalles[0].Subtotal);
        Assert.Equal(30000m, result.Detalles[1].Subtotal);
        Assert.Equal(80000m, result.Total);
    }

    [Fact]
    public async Task ObtenerVentas_SinFiltros_DevuelveTodas()
    {
        var service = _service;
        await service.CreateAsync(VentaValida("Efectivo"));
        await service.CreateAsync(VentaValida("Tarjeta"));
        await service.CreateAsync(VentaValida("Transferencia"));

        var result = await service.GetAllAsync();

        Assert.Equal(3, result.Count());
    }

    [Fact]
    public async Task ObtenerVentas_FiltrandoPorEstado_DevuelveSoloEseEstado()
    {
        var service = _service;
        var pendiente = await service.CreateAsync(VentaValida("Efectivo"));
        var confirmada = await service.CreateAsync(VentaValida("Tarjeta"));
        await service.ConfirmarAsync(confirmada.Id);
        var anulada = await service.CreateAsync(VentaValida("Transferencia"));
        await service.AnularAsync(anulada.Id, new AnularVentaDto { Motivo = "error de registro" });

        var pendientes = await service.GetAllAsync(estado: "Pendiente");
        var confirmadas = await service.GetAllAsync(estado: "Confirmada");
        var anuladas = await service.GetAllAsync(estado: "Anulada");

        Assert.Single(pendientes);
        Assert.Single(confirmadas);
        Assert.Single(anuladas);
        Assert.Equal(pendiente.Id, pendientes.First().Id);
        Assert.Equal(confirmada.Id, confirmadas.First().Id);
        Assert.Equal(anulada.Id, anuladas.First().Id);
    }

    [Theory]
    [InlineData("Pagada")]
    [InlineData("Confirmad")]
    [InlineData("Anulado")]
    public async Task ObtenerVentas_EstadoInvalido_LanzaArgumentException(string estado)
    {
        var service = _service;
        await service.CreateAsync(VentaValida());

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAllAsync(estado: estado));
    }

    [Fact]
    public async Task ObtenerVentas_EstadoVacio_SeTrataComoSinFiltro()
    {
        var service = _service;
        await service.CreateAsync(VentaValida());
        await service.CreateAsync(VentaValida());

        var result = await service.GetAllAsync(estado: "");

        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task ObtenerVentas_FiltrandoPorFecha_DevuelveVentasDelDia()
    {
        var service = _service;
        var creada = await service.CreateAsync(VentaValida());

        // El día calendario local del negocio (UTC-5); el filtro interpreta ?fecha=
        // como "día local" y lo convierte a un rango UTC.
        var hoyLocal = DateTime.UtcNow.AddHours(-5).Date;
        var result = await service.GetAllAsync(fecha: hoyLocal);

        Assert.Contains(result, v => v.Id == creada.Id);
    }

    [Fact]
    public async Task ObtenerVentas_FiltrandoPorFechaSinVentas_DevuelveVacio()
    {
        var service = _service;
        await service.CreateAsync(VentaValida());

        var result = await service.GetAllAsync(fecha: new DateTime(2020, 1, 1));

        Assert.Empty(result);
    }

    [Fact]
    public async Task ObtenerVentas_FiltroCombinadoEstadoYFecha_DevuelveCoincidencias()
    {
        var service = _service;
        var confirmada = await service.CreateAsync(VentaValida("Efectivo"));
        await service.ConfirmarAsync(confirmada.Id);
        await service.CreateAsync(VentaValida("Tarjeta"));

        var hoyLocal = DateTime.UtcNow.AddHours(-5).Date;
        var result = await service.GetAllAsync(estado: "Confirmada", fecha: hoyLocal);

        Assert.Single(result);
        Assert.Equal(confirmada.Id, result.First().Id);
    }

    [Fact]
    public async Task ObtenerResumen_ConVentasEnDistintosEstados_CalculaTotales()
    {
        var service = _service;
        var c1 = await service.CreateAsync(VentaValida("Efectivo"));
        await service.ConfirmarAsync(c1.Id);
        var c2 = await service.CreateAsync(VentaValida(_varianteId, 3, 10000m, "Tarjeta"));
        await service.ConfirmarAsync(c2.Id);
        var a = await service.CreateAsync(VentaValida(_varianteId, 1, 99999m, "Transferencia"));
        await service.AnularAsync(a.Id, new AnularVentaDto { Motivo = "error de registro" });
        await service.CreateAsync(VentaValida("Efectivo"));

        var resumen = await service.GetResumenAsync();

        Assert.Equal(4, resumen.CantidadVentas);
        Assert.Equal(2, resumen.VentasConfirmadas);
        Assert.Equal(1, resumen.VentasAnuladas);
        Assert.Equal(80000m, resumen.TotalVentasConfirmadas);
    }

    [Fact]
    public async Task ObtenerResumen_NoIncluyeVentasAnuladasEnTotalConfirmado()
    {
        var service = _service;
        var confirmada = await service.CreateAsync(VentaValida("Efectivo"));
        await service.ConfirmarAsync(confirmada.Id);
        var anulada = await service.CreateAsync(VentaValida(_varianteId, 1, 99999m, "Tarjeta"));
        await service.AnularAsync(anulada.Id, new AnularVentaDto { Motivo = "registro erroneo" });

        var resumen = await service.GetResumenAsync();

        Assert.Equal(1, resumen.VentasAnuladas);
        Assert.Equal(50000m, resumen.TotalVentasConfirmadas);
        Assert.Equal(2, resumen.CantidadVentas);
    }

    // =============================================================
    // PROBLEMA 1: validación y descuento de stock al confirmar
    // =============================================================

    [Fact]
    public async Task ConfirmarVenta_ConStockSuficiente_DescuentaStockYRegistraMovimiento()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(5);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m));

        var result = await service.ConfirmarAsync(created.Id);

        Assert.Equal("Confirmada", result.Estado);
        Assert.Equal(3, (await _context.Variantes.FindAsync(varianteId))!.Stock);

        var movimiento = await _context.MovimientosStock.SingleAsync();
        Assert.Equal(varianteId, movimiento.VarianteId);
        Assert.Equal("venta", movimiento.Tipo);
        Assert.Equal(-2, movimiento.Cantidad);
        Assert.Equal("venta", movimiento.ReferenciaTipo);
        Assert.Equal(created.Id, movimiento.ReferenciaId);
        Assert.Equal("sistema", movimiento.Usuario);
    }

    [Fact]
    public async Task ConfirmarVenta_ConStockInsuficiente_FallaYVentaPermanecePendiente()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(1);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m));

        var ex = await Assert.ThrowsAsync<StockInsuficienteException>(() => service.ConfirmarAsync(created.Id));

        Assert.Equal(varianteId, ex.VarianteId);
        Assert.Equal(1, ex.Disponible);
        Assert.Equal(2, ex.Solicitado);

        var obtenida = await service.GetByIdAsync(created.Id);
        Assert.NotNull(obtenida);
        Assert.Equal("Pendiente", obtenida.Estado);
        // Sin efecto colateral: la variante mantiene su stock, no hay movimientos ni factura.
        Assert.Equal(1, (await _context.Variantes.FindAsync(varianteId))!.Stock);
        Assert.Empty(await _context.MovimientosStock.ToListAsync());
        Assert.Empty(await _context.Facturas.ToListAsync());
    }

    [Fact]
    public async Task ConfirmarVenta_ConVariasVariantes_SinStockEnUna_NoDescuentaNinguna()
    {
        var service = _service;
        var v1 = await CrearVarianteConStockAsync(10);
        var v2 = await CrearVarianteConStockAsync(1);
        var dto = new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto>
            {
                new() { VarianteId = v1, Cantidad = 3, PrecioUnitario = 10000m },
                new() { VarianteId = v2, Cantidad = 2, PrecioUnitario = 20000m }
            }
        };
        var created = await service.CreateAsync(dto);

        await Assert.ThrowsAsync<StockInsuficienteException>(() => service.ConfirmarAsync(created.Id));

        // Requisito: no hay descuentos parciales. Ninguna variante cambió.
        Assert.Equal(10, (await _context.Variantes.FindAsync(v1))!.Stock);
        Assert.Equal(1, (await _context.Variantes.FindAsync(v2))!.Stock);
        Assert.Empty(await _context.MovimientosStock.ToListAsync());
        Assert.Empty(await _context.Facturas.ToListAsync());
    }

    [Fact]
    public async Task ConfirmarVenta_ConVarianteInexistente_FallaDeFormaControlada()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida(99999, 1, 25000m));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ConfirmarAsync(created.Id));

        var obtenida = await service.GetByIdAsync(created.Id);
        Assert.NotNull(obtenida);
        Assert.Equal("Pendiente", obtenida.Estado);
        Assert.Empty(await _context.MovimientosStock.ToListAsync());
        Assert.Empty(await _context.Facturas.ToListAsync());
    }

    [Fact]
    public async Task ConfirmarVenta_YaConfirmada_NoVuelveADescontar()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(5);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m));
        await service.ConfirmarAsync(created.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmarAsync(created.Id));

        // Se descontó una sola vez: stock 5->3, un movimiento 'venta' y una factura.
        Assert.Equal(3, (await _context.Variantes.FindAsync(varianteId))!.Stock);
        Assert.Single(await _context.MovimientosStock.ToListAsync());
        Assert.Single(await _context.Facturas.ToListAsync());
    }

    [Fact]
    public async Task ConfirmarVenta_GeneraFacturaEmitida()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(10);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m));

        await service.ConfirmarAsync(created.Id);

        var factura = await _context.Facturas.FirstOrDefaultAsync(f => f.VentaId == created.Id);
        Assert.NotNull(factura);
        Assert.Equal("emitida", factura.Estado);
        Assert.Equal("FAC-000001", factura.Numero);
        Assert.Equal(50000m, factura.Subtotal);
        Assert.Equal(0m, factura.Impuesto);
        Assert.Equal(50000m, factura.Total);
    }

    [Fact]
    public async Task ConfirmarVenta_FacturaQuedaPersistida()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(10);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m));

        await service.ConfirmarAsync(created.Id);

        // Un contexto nuevo sobre la misma base InMemory comprueba que la factura
        // quedó persistida y no solo en memoria del contexto de prueba.
        var options = new DbContextOptionsBuilder<ErpModaDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        await using var otroContexto = new ErpModaDbContext(options);

        var factura = await otroContexto.Facturas.FirstOrDefaultAsync(f => f.VentaId == created.Id);
        var venta = await otroContexto.Ventas.Include(v => v.Detalles).FirstOrDefaultAsync(v => v.Id == created.Id);

        Assert.NotNull(factura);
        Assert.Equal("emitida", factura!.Estado);
        Assert.Equal("FAC-000001", factura.Numero);
        Assert.NotNull(venta);
        Assert.Equal("Confirmada", venta!.Estado.ToString());
        Assert.Single(venta.Detalles);
    }

    // =============================================================
    // PROBLEMA 2: anulación y restauración de stock
    // =============================================================

    [Fact]
    public async Task AnularVentaConfirmada_RestauraStock()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(10);
        var created = await service.CreateAsync(VentaValida(varianteId, 3, 25000m));
        await service.ConfirmarAsync(created.Id);
        // Stock 10 -> 7 tras confirmar.
        Assert.Equal(7, (await _context.Variantes.FindAsync(varianteId))!.Stock);

        await service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "Devolución del cliente" });

        var obtenida = await service.GetByIdAsync(created.Id);
        Assert.Equal("Anulada", obtenida!.Estado);
        Assert.Equal("Devolución del cliente", obtenida.MotivoAnulacion);
        Assert.Equal(10, (await _context.Variantes.FindAsync(varianteId))!.Stock);

        var movimiento = await _context.MovimientosStock.FirstOrDefaultAsync(m => m.Tipo == "anulacion_venta");
        Assert.NotNull(movimiento);
        Assert.Equal(varianteId, movimiento!.VarianteId);
        Assert.Equal(3, movimiento.Cantidad);
        Assert.Equal("venta", movimiento.ReferenciaTipo);
        Assert.Equal(created.Id, movimiento.ReferenciaId);
        Assert.Equal("Devolución del cliente", movimiento.Motivo);

        var factura = await _context.Facturas.FirstOrDefaultAsync(f => f.VentaId == created.Id);
        Assert.NotNull(factura);
        Assert.Equal("anulada", factura!.Estado);
    }

    [Fact]
    public async Task AnularVentaYaAnulada_NoRestauraDosVeces()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(5);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m));
        await service.ConfirmarAsync(created.Id);
        await service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "primer motivo" });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "segundo motivo" }));

        // El stock se restauró una sola vez: 5 -> 3 (confirmar) -> 5 (anular).
        Assert.Equal(5, (await _context.Variantes.FindAsync(varianteId))!.Stock);
        Assert.Single(await _context.MovimientosStock.Where(m => m.Tipo == "anulacion_venta").ToListAsync());
    }

    [Fact]
    public async Task AnularVentaPendiente_NoRestauraStockPorqueNuncaSeDesconto()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(7);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m));

        await service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "venta no confirmada" });

        var obtenida = await service.GetByIdAsync(created.Id);
        Assert.Equal("Anulada", obtenida!.Estado);
        Assert.Equal(7, (await _context.Variantes.FindAsync(varianteId))!.Stock);
        Assert.Empty(await _context.MovimientosStock.ToListAsync());
    }

    [Fact]
    public async Task ConfirmarVenta_Factura_TieneDatosCorrectosYEsUnica()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(10);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m));

        await service.ConfirmarAsync(created.Id);

        // Existe factura asociada a la venta
        var factura = await _context.Facturas.FirstOrDefaultAsync(f => f.VentaId == created.Id);
        Assert.NotNull(factura);

        // Cargar la venta completa con subtotal/impuesto para comparar
        var venta = await _context.Ventas.FirstOrDefaultAsync(v => v.Id == created.Id);
        Assert.NotNull(venta);

        // VentaId correcto
        Assert.Equal(created.Id, factura!.VentaId);

        // Numero tiene valor válido y formato esperado
        Assert.False(string.IsNullOrWhiteSpace(factura.Numero));
        Assert.StartsWith("FAC-", factura.Numero);

        // Totales corresponden a la venta (desde la entidad, no el DTO)
        Assert.Equal(venta!.Subtotal, factura.Subtotal);
        Assert.Equal(venta.Impuesto, factura.Impuesto);
        Assert.Equal(venta.Total, factura.Total);

        // Estado inicial es "emitida"
        Assert.Equal("emitida", factura.Estado);

        // Número es único en la tabla
        var duplicadas = await _context.Facturas.CountAsync(f => f.Numero == factura.Numero);
        Assert.Equal(1, duplicadas);
    }
}