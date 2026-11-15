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

    private const string UsuarioPrueba = "usuario-prueba";

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
        var result = await _service.CreateAsync(VentaValida(), UsuarioPrueba);

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
        var created = await service.CreateAsync(VentaValida(), UsuarioPrueba);

        var result = await service.GetByIdAsync(created.Id, UsuarioPrueba, true);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("Pendiente", result.Estado);
    }

    [Fact]
    public async Task CrearVenta_ConListaDeDetallesVacia_LanzaArgumentException()
    {
        var service = _service;
        var dto = new CrearVentaDto { MetodoPago = "Efectivo", Detalles = new List<DetalleCrearDto>() };

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto, UsuarioPrueba));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CrearVenta_CantidadInvalida_LanzaArgumentException(int cantidad)
    {
        var service = _service;
        var dto = VentaValida();
        dto.Detalles[0].Cantidad = cantidad;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto, UsuarioPrueba));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task CrearVenta_PrecioInvalido_LanzaArgumentException(decimal precio)
    {
        var service = _service;
        var dto = VentaValida();
        dto.Detalles[0].PrecioUnitario = precio;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto, UsuarioPrueba));
    }

    [Theory]
    [InlineData("cheque")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(null)]
    public async Task CrearVenta_MetodoPagoInvalido_LanzaArgumentException(string? metodoPago)
    {
        var service = _service;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(VentaValida(metodoPago), UsuarioPrueba));
    }

    [Fact]
    public async Task CrearVenta_ConVarianteInexistente_LanzaArgumentException()
    {
        var service = _service;
        const int varianteInexistente = 99999;
        var dto = new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto>
            {
                new() { VarianteId = varianteInexistente, Cantidad = 1, PrecioUnitario = 10000m }
            }
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto, UsuarioPrueba));
        // El mensaje debe identificar el ID de la variante inexistente.
        Assert.Contains(varianteInexistente.ToString(), ex.Message);
    }

    [Fact]
    public async Task ConfirmarVenta_EnPendiente_CambiaAConfirmada()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida(), UsuarioPrueba);

        var result = await service.ConfirmarAsync(created.Id, UsuarioPrueba, true);

        Assert.Equal("Confirmada", result.Estado);
    }

    [Fact]
    public async Task ConfirmarVenta_Inexistente_LanzaKeyNotFoundException()
    {
        var service = _service;

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ConfirmarAsync(999, UsuarioPrueba, true));
    }

    [Fact]
    public async Task ConfirmarVenta_YaConfirmada_LanzaInvalidOperationException()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida(), UsuarioPrueba);
        await service.ConfirmarAsync(created.Id, UsuarioPrueba, true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmarAsync(created.Id, UsuarioPrueba, true));
    }

    [Fact]
    public async Task AnularVenta_CambiaAAnulada_ConservaMotivoYNoElimina()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida(), UsuarioPrueba);

        var result = await service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "Error de registro" }, UsuarioPrueba, true);

        Assert.Equal("Anulada", result.Estado);
        Assert.Equal("Error de registro", result.MotivoAnulacion);

        var obtenida = await service.GetByIdAsync(created.Id, UsuarioPrueba, true);
        Assert.NotNull(obtenida);
        Assert.Equal("Anulada", obtenida.Estado);
    }

    [Fact]
    public async Task AnularVenta_Inexistente_LanzaKeyNotFoundException()
    {
        var service = _service;

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.AnularAsync(999, new AnularVentaDto { Motivo = "prueba" }, UsuarioPrueba, true));
    }

    [Fact]
    public async Task AnularVenta_YaAnulada_LanzaInvalidOperationException()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida(), UsuarioPrueba);
        await service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "primer motivo" }, UsuarioPrueba, true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "segundo motivo" }, UsuarioPrueba, true));
    }

    [Fact]
    public async Task AnularVenta_SinMotivo_LanzaArgumentException()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida(), UsuarioPrueba);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "  " }, UsuarioPrueba, true));
    }

    [Fact]
    public async Task CrearVenta_ConVariosDetalles_CalculaSubtotalesYTotal()
    {
        var service = _service;
        // Crear dos variantes reales para que la validación de existencia no rechace la venta.
        var v1 = await CrearVarianteConStockAsync(10);
        var v2 = await CrearVarianteConStockAsync(10);
        var dto = new CrearVentaDto
        {
            MetodoPago = "Tarjeta",
            Detalles = new List<DetalleCrearDto>
            {
                new() { VarianteId = v1, Cantidad = 2, PrecioUnitario = 25000m },
                new() { VarianteId = v2, Cantidad = 3, PrecioUnitario = 10000m }
            }
        };

        var result = await service.CreateAsync(dto, UsuarioPrueba);

        Assert.Equal(2, result.Detalles.Count);
        Assert.Equal(50000m, result.Detalles[0].Subtotal);
        Assert.Equal(30000m, result.Detalles[1].Subtotal);
        Assert.Equal(80000m, result.Total);
    }

    [Fact]
    public async Task ObtenerVentas_SinFiltros_DevuelveTodas()
    {
        var service = _service;
        await service.CreateAsync(VentaValida("Efectivo"), UsuarioPrueba);
        await service.CreateAsync(VentaValida("Tarjeta"), UsuarioPrueba);
        await service.CreateAsync(VentaValida("Transferencia"), UsuarioPrueba);

        var result = await service.GetAllAsync(usuarioActual: UsuarioPrueba, esAdministrador: true);

        Assert.Equal(3, result.Count());
    }

    [Fact]
    public async Task ObtenerVentas_FiltrandoPorEstado_DevuelveSoloEseEstado()
    {
        var service = _service;
        var pendiente = await service.CreateAsync(VentaValida("Efectivo"), UsuarioPrueba);
        var confirmada = await service.CreateAsync(VentaValida("Tarjeta"), UsuarioPrueba);
        await service.ConfirmarAsync(confirmada.Id, UsuarioPrueba, true);
        var anulada = await service.CreateAsync(VentaValida("Transferencia"), UsuarioPrueba);
        await service.AnularAsync(anulada.Id, new AnularVentaDto { Motivo = "error de registro" }, UsuarioPrueba, true);

        var pendientes = await service.GetAllAsync(estado: "Pendiente", usuarioActual: UsuarioPrueba, esAdministrador: true);
        var confirmadas = await service.GetAllAsync(estado: "Confirmada", usuarioActual: UsuarioPrueba, esAdministrador: true);
        var anuladas = await service.GetAllAsync(estado: "Anulada", usuarioActual: UsuarioPrueba, esAdministrador: true);

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
        await service.CreateAsync(VentaValida(), UsuarioPrueba);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAllAsync(estado: estado, usuarioActual: UsuarioPrueba, esAdministrador: true));
    }

    [Fact]
    public async Task ObtenerVentas_EstadoVacio_SeTrataComoSinFiltro()
    {
        var service = _service;
        await service.CreateAsync(VentaValida(), UsuarioPrueba);
        await service.CreateAsync(VentaValida(), UsuarioPrueba);

        var result = await service.GetAllAsync(estado: "", usuarioActual: UsuarioPrueba, esAdministrador: true);

        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task ObtenerVentas_FiltrandoPorFecha_DevuelveVentasDelDia()
    {
        var service = _service;
        var creada = await service.CreateAsync(VentaValida(), UsuarioPrueba);

        // El día calendario local del negocio (UTC-5); el filtro interpreta ?fecha=
        // como "día local" y lo convierte a un rango UTC.
        var hoyLocal = DateTime.UtcNow.AddHours(-5).Date;
        var result = await service.GetAllAsync(fecha: hoyLocal, usuarioActual: UsuarioPrueba, esAdministrador: true);

        Assert.Contains(result, v => v.Id == creada.Id);
    }

    [Fact]
    public async Task ObtenerVentas_FiltrandoPorFechaSinVentas_DevuelveVacio()
    {
        var service = _service;
        await service.CreateAsync(VentaValida(), UsuarioPrueba);

        var result = await service.GetAllAsync(fecha: new DateTime(2020, 1, 1), usuarioActual: UsuarioPrueba, esAdministrador: true);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ObtenerVentas_FiltroCombinadoEstadoYFecha_DevuelveCoincidencias()
    {
        var service = _service;
        var confirmada = await service.CreateAsync(VentaValida("Efectivo"), UsuarioPrueba);
        await service.ConfirmarAsync(confirmada.Id, UsuarioPrueba, true);
        await service.CreateAsync(VentaValida("Tarjeta"), UsuarioPrueba);

        var hoyLocal = DateTime.UtcNow.AddHours(-5).Date;
        var result = await service.GetAllAsync(estado: "Confirmada", fecha: hoyLocal, usuarioActual: UsuarioPrueba, esAdministrador: true);

        Assert.Single(result);
        Assert.Equal(confirmada.Id, result.First().Id);
    }

    [Fact]
    public async Task ObtenerResumen_ConVentasEnDistintosEstados_CalculaTotales()
    {
        var service = _service;
        var c1 = await service.CreateAsync(VentaValida("Efectivo"), UsuarioPrueba);
        await service.ConfirmarAsync(c1.Id, UsuarioPrueba, true);
        var c2 = await service.CreateAsync(VentaValida(_varianteId, 3, 10000m, "Tarjeta"), UsuarioPrueba);
        await service.ConfirmarAsync(c2.Id, UsuarioPrueba, true);
        var a = await service.CreateAsync(VentaValida(_varianteId, 1, 99999m, "Transferencia"), UsuarioPrueba);
        await service.AnularAsync(a.Id, new AnularVentaDto { Motivo = "error de registro" }, UsuarioPrueba, true);
        await service.CreateAsync(VentaValida("Efectivo"), UsuarioPrueba);

        var resumen = await service.GetResumenAsync(UsuarioPrueba, true);

        Assert.Equal(4, resumen.CantidadVentas);
        Assert.Equal(2, resumen.VentasConfirmadas);
        Assert.Equal(1, resumen.VentasAnuladas);
        Assert.Equal(80000m, resumen.TotalVentasConfirmadas);
    }

    [Fact]
    public async Task ObtenerResumen_NoIncluyeVentasAnuladasEnTotalConfirmado()
    {
        var service = _service;
        var confirmada = await service.CreateAsync(VentaValida("Efectivo"), UsuarioPrueba);
        await service.ConfirmarAsync(confirmada.Id, UsuarioPrueba, true);
        var anulada = await service.CreateAsync(VentaValida(_varianteId, 1, 99999m, "Tarjeta"), UsuarioPrueba);
        await service.AnularAsync(anulada.Id, new AnularVentaDto { Motivo = "registro erroneo" }, UsuarioPrueba, true);

        var resumen = await service.GetResumenAsync(UsuarioPrueba, true);

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
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m), UsuarioPrueba);

        var result = await service.ConfirmarAsync(created.Id, UsuarioPrueba, true);

        Assert.Equal("Confirmada", result.Estado);
        Assert.Equal(3, (await _context.Variantes.FindAsync(varianteId))!.Stock);

        var movimiento = await _context.MovimientosStock.SingleAsync();
        Assert.Equal(varianteId, movimiento.VarianteId);
        Assert.Equal("venta", movimiento.Tipo);
        Assert.Equal(-2, movimiento.Cantidad);
        Assert.Equal("venta", movimiento.ReferenciaTipo);
        Assert.Equal(created.Id, movimiento.ReferenciaId);
        Assert.Equal(UsuarioPrueba, movimiento.Usuario);
    }

    [Fact]
    public async Task ConfirmarVenta_ConStockInsuficiente_FallaYVentaPermanecePendiente()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(1);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m), UsuarioPrueba);

        var ex = await Assert.ThrowsAsync<StockInsuficienteException>(() => service.ConfirmarAsync(created.Id, UsuarioPrueba, true));

        Assert.Equal(varianteId, ex.VarianteId);
        Assert.Equal(1, ex.Disponible);
        Assert.Equal(2, ex.Solicitado);

        var obtenida = await service.GetByIdAsync(created.Id, UsuarioPrueba, true);
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
        var created = await service.CreateAsync(dto, UsuarioPrueba);

        await Assert.ThrowsAsync<StockInsuficienteException>(() => service.ConfirmarAsync(created.Id, UsuarioPrueba, true));

        // Requisito: no hay descuentos parciales. Ninguna variante cambió.
        Assert.Equal(10, (await _context.Variantes.FindAsync(v1))!.Stock);
        Assert.Equal(1, (await _context.Variantes.FindAsync(v2))!.Stock);
        Assert.Empty(await _context.MovimientosStock.ToListAsync());
        Assert.Empty(await _context.Facturas.ToListAsync());
    }

    [Fact]
    public async Task ConfirmarVenta_ConVarianteInexistente_FallaDeFormaControlada()
    {
        // Con la validación de variantes en CreateAsync, no es posible crear una venta
        // que referencie una variante inexistente. El rechazo ocurre antes de persistir.
        // Esta prueba verifica que CreateAsync lanza ArgumentException y que la BD queda
        // vacía (sin ventas, movimientos ni facturas).
        var service = _service;
        const int varianteInexistente = 99999;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(VentaValida(varianteInexistente, 1, 25000m), UsuarioPrueba));

        // La venta nunca se guardó, así que no hay nada que confirmar.
        Assert.Empty(await _context.Ventas.ToListAsync());
        Assert.Empty(await _context.MovimientosStock.ToListAsync());
        Assert.Empty(await _context.Facturas.ToListAsync());
    }

    [Fact]
    public async Task ConfirmarVenta_YaConfirmada_NoVuelveADescontar()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(5);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m), UsuarioPrueba);
        await service.ConfirmarAsync(created.Id, UsuarioPrueba, true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmarAsync(created.Id, UsuarioPrueba, true));

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
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m), UsuarioPrueba);

        await service.ConfirmarAsync(created.Id, UsuarioPrueba, true);

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
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m), UsuarioPrueba);

        await service.ConfirmarAsync(created.Id, UsuarioPrueba, true);

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
        var created = await service.CreateAsync(VentaValida(varianteId, 3, 25000m), UsuarioPrueba);
        await service.ConfirmarAsync(created.Id, UsuarioPrueba, true);
        // Stock 10 -> 7 tras confirmar.
        Assert.Equal(7, (await _context.Variantes.FindAsync(varianteId))!.Stock);

        await service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "Devolución del cliente" }, UsuarioPrueba, true);

        var obtenida = await service.GetByIdAsync(created.Id, UsuarioPrueba, true);
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
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m), UsuarioPrueba);
        await service.ConfirmarAsync(created.Id, UsuarioPrueba, true);
        await service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "primer motivo" }, UsuarioPrueba, true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "segundo motivo" }, UsuarioPrueba, true));

        // El stock se restauró una sola vez: 5 -> 3 (confirmar) -> 5 (anular).
        Assert.Equal(5, (await _context.Variantes.FindAsync(varianteId))!.Stock);
        Assert.Single(await _context.MovimientosStock.Where(m => m.Tipo == "anulacion_venta").ToListAsync());
    }

    [Fact]
    public async Task AnularVentaPendiente_NoRestauraStockPorqueNuncaSeDesconto()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(7);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m), UsuarioPrueba);

        await service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "venta no confirmada" }, UsuarioPrueba, true);

        var obtenida = await service.GetByIdAsync(created.Id, UsuarioPrueba, true);
        Assert.Equal("Anulada", obtenida!.Estado);
        Assert.Equal(7, (await _context.Variantes.FindAsync(varianteId))!.Stock);
        Assert.Empty(await _context.MovimientosStock.ToListAsync());
    }

    [Fact]
    public async Task ConfirmarVenta_Factura_TieneDatosCorrectosYEsUnica()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(10);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m), UsuarioPrueba);

        await service.ConfirmarAsync(created.Id, UsuarioPrueba, true);

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

    // =============================================================
    // REGRESIÓN: configuración EF Core — sin FK sombra VarianteId1
    // =============================================================

    /// <summary>
    /// Verifica que la relación Variante → MovimientoStock esté configurada
    /// con exactamente una FK ("variante_id") y que EF Core NO haya creado
    /// una propiedad sombra "VarianteId1" producto de una doble definición de
    /// relación (bug detectado en PR #38).
    ///
    /// NOTA: InMemory no valida el DDL real de PostgreSQL. Este test comprueba
    /// el modelo EF Core (metadata) pero NO garantiza que la migración sea
    /// correcta contra PostgreSQL. Para esa garantía se requiere ejecutar
    /// contra la base real (ver issue #37).
    /// </summary>
    [Fact]
    public void ModeloEfCore_MovimientoStock_NoTienePropiedadSombraVarianteId1()
    {
        var entityType = _context.Model.FindEntityType(typeof(Inventario.Models.MovimientoStock));
        Assert.NotNull(entityType);

        // No debe existir una propiedad sombra llamada "VarianteId1".
        var propiedadSombra = entityType!.FindProperty("VarianteId1");
        Assert.Null(propiedadSombra);

        // Debe existir exactamente una FK cuya columna sea "variante_id" / "VarianteId".
        var fksHaciaVariante = entityType.GetForeignKeys()
            .Where(fk => fk.PrincipalEntityType.ClrType == typeof(Inventario.Models.Variante))
            .ToList();
        Assert.Single(fksHaciaVariante);

        // La FK debe apuntar a la propiedad de navegación Variante (no a una sombra).
        var fk = fksHaciaVariante[0];
        Assert.Equal("VarianteId", fk.Properties[0].Name);
        Assert.NotNull(fk.DependentToPrincipal);   // la navegación Variante existe
        Assert.Equal("Variante", fk.DependentToPrincipal!.Name);
    }
    [Fact]
    public async Task Ownership_CrearVenta_AsignaVendedorIdDesdeIdentidad()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida(), "vendedor-1");

        var venta = await _context.Ventas.FindAsync(created.Id);
        Assert.NotNull(venta);
        Assert.Equal("vendedor-1", venta!.VendedorId);
    }

    [Fact]
    public async Task Ownership_Vendedor_ListadoSoloIncluyeSusVentas()
    {
        var service = _service;
        var propia = await service.CreateAsync(VentaValida(), "vendedor-1");
        await service.CreateAsync(VentaValida(), "vendedor-2");

        var result = (await service.GetAllAsync(usuarioActual: "vendedor-1", esAdministrador: false)).ToList();

        Assert.Single(result);
        Assert.Equal(propia.Id, result[0].Id);
    }

    [Fact]
    public async Task Ownership_Vendedor_ConsultaVentaPropia_DevuelveVenta()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida(), "vendedor-1");

        var result = await service.GetByIdAsync(created.Id, "vendedor-1", false);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result!.Id);
    }

    [Fact]
    public async Task Ownership_Vendedor_ConsultaVentaAjena_DevuelveNull()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida(), "vendedor-1");

        var result = await service.GetByIdAsync(created.Id, "vendedor-2", false);

        Assert.Null(result);
    }

    [Fact]
    public async Task Ownership_OperacionSinIdentidad_PermiteCompatibilidad()
    {
        var service = _service;
        var created = await service.CreateAsync(VentaValida(), "vendedor-1");

        var createdSinIdentidad = await service.CreateAsync(VentaValida(), "");
        Assert.Null((await _context.Ventas.FindAsync(createdSinIdentidad.Id))!.VendedorId);

        await service.GetAllAsync(usuarioActual: "", esAdministrador: false);
        Assert.NotNull(await service.GetByIdAsync(created.Id, "", false));

        await service.ConfirmarAsync(created.Id, "", false);
        await service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "motivo" }, "", false);

        var resumen = await service.GetResumenAsync("", true);
        Assert.NotNull(resumen);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetResumenAsync(UsuarioPrueba, false));
    }

    [Fact]
    public async Task Ownership_Vendedor_ConfirmaVentaAjena_NoModificaStock()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(10);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m), "vendedor-1");
        var stockInicial = (await _context.Variantes.FindAsync(varianteId))!.Stock;

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ConfirmarAsync(created.Id, "vendedor-2", false));

        Assert.Equal(stockInicial, (await _context.Variantes.FindAsync(varianteId))!.Stock);
        Assert.Empty(await _context.MovimientosStock.ToListAsync());
    }

    [Fact]
    public async Task Ownership_Vendedor_AnulaVentaAjena_NoRestauraStock()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(10);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m), "vendedor-1");
        await service.ConfirmarAsync(created.Id, "vendedor-1", true);

        var movimientosAntes = await _context.MovimientosStock.CountAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "motivo" }, "vendedor-2", false));

        Assert.Equal(movimientosAntes, await _context.MovimientosStock.CountAsync());
    }

    [Fact]
    public async Task Ownership_Administrador_PuedeOperarVentaSinPropietario()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(10);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m), "admin");

        var venta = await _context.Ventas.FindAsync(created.Id);
        Assert.NotNull(venta);
        venta!.VendedorId = null;
        await _context.SaveChangesAsync();

        var obtenida = await service.GetByIdAsync(created.Id, "admin", true);
        Assert.NotNull(obtenida);

        var confirmada = await service.ConfirmarAsync(created.Id, "admin", true);
        Assert.Equal("Confirmada", confirmada.Estado);

        var anulada = await service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "motivo admin" }, "admin", true);
        Assert.Equal("Anulada", anulada.Estado);
    }

    [Fact]
    public async Task Ownership_Confirmar_RegistraIdentidadEnMovimiento()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(5);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m), "vendedor-identidad");

        await service.ConfirmarAsync(created.Id, "vendedor-identidad", true);

        var movimiento = await _context.MovimientosStock.SingleAsync();
        Assert.Equal("vendedor-identidad", movimiento.Usuario);
    }

    [Fact]
    public async Task Ownership_Vendedor_NoVeVentasAntiguasSinPropietario()
    {
        var service = _service;
        var creada = await service.CreateAsync(VentaValida(), "vendedor-1");

        var venta = await _context.Ventas.FindAsync(creada.Id);
        Assert.NotNull(venta);
        venta!.VendedorId = null;
        await _context.SaveChangesAsync();

        var resultado = (await service.GetAllAsync(usuarioActual: "vendedor-1", esAdministrador: false)).ToList();
        Assert.Empty(resultado);
    }
    [Fact]
    public async Task Ownership_Administrador_GetAll_IncluyeVentasDeDistintosPropietariosYLegacy()
    {
        var service = _service;
        await service.CreateAsync(VentaValida(), "vendedor-1");
        await service.CreateAsync(VentaValida(), "vendedor-2");
        var propiaAdmin = await service.CreateAsync(VentaValida(), "admin");

        var ventaLegacy = await _context.Ventas.FindAsync(propiaAdmin.Id);
        Assert.NotNull(ventaLegacy);
        ventaLegacy!.VendedorId = null;
        await _context.SaveChangesAsync();

        var result = (await service.GetAllAsync(usuarioActual: "admin", esAdministrador: true)).ToList();

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task Ownership_GetResumen_RequiereIdentidadAdministrativa()
    {
        var service = _service;
        await service.CreateAsync(VentaValida(), "vendedor-1");

        var resumenSinIdentidad = await service.GetResumenAsync("", true);
        Assert.NotNull(resumenSinIdentidad);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetResumenAsync(UsuarioPrueba, false));
    }

    [Fact]
    public async Task Ownership_Anular_RegistraIdentidadEnMovimiento()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(5);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m), "vendedor-anulacion");
        await service.ConfirmarAsync(created.Id, "vendedor-anulacion", true);

        await service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "motivo anulacion" }, "vendedor-anulacion", true);

        var movimiento = await _context.MovimientosStock.SingleAsync(m => m.Tipo == "anulacion_venta");
        Assert.Equal("vendedor-anulacion", movimiento.Usuario);
    }

    [Fact]
    public async Task Ownership_AnulacionDenegada_NoRestauraStockNiGeneraMovimiento()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(10);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m), "vendedor-1");
        await service.ConfirmarAsync(created.Id, "vendedor-1", true);

        var stockConfirmado = (await _context.Variantes.FindAsync(varianteId))!.Stock;
        var movimientosAntes = await _context.MovimientosStock.CountAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "motivo" }, "vendedor-2", false));

        Assert.Equal(stockConfirmado, (await _context.Variantes.FindAsync(varianteId))!.Stock);
        Assert.Equal(movimientosAntes, await _context.MovimientosStock.CountAsync());
    }

    [Fact]
    public async Task Compatibilidad_SinIdentidad_CreaVentaConVendedorIdNull()
    {
        var service = _service;

        var created = await service.CreateAsync(VentaValida(), null);

        var venta = await _context.Ventas.FindAsync(created.Id);
        Assert.NotNull(venta);
        Assert.Null(venta!.VendedorId);
    }

    [Fact]
    public async Task Compatibilidad_SinIdentidad_ListadoNoAplicaFiltroDePropiedad()
    {
        var service = _service;
        await service.CreateAsync(VentaValida(), "vendedor-1");
        await service.CreateAsync(VentaValida(), "vendedor-2");

        var todas = (await service.GetAllAsync(usuarioActual: null, esAdministrador: false)).ToList();

        Assert.Equal(2, todas.Count);
    }

    [Fact]
    public async Task Compatibilidad_ConfirmarSinIdentidad_RegistraMovimientoConSistema()
    {
        var service = _service;
        var varianteId = await CrearVarianteConStockAsync(5);
        var created = await service.CreateAsync(VentaValida(varianteId, 2, 25000m), null);

        await service.ConfirmarAsync(created.Id, null, false);

        var movimiento = await _context.MovimientosStock.SingleAsync();
        Assert.Equal("sistema", movimiento.Usuario);
    }
}




