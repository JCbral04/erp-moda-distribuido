using ErpModa.Api.Ventas.DTOs;
using ErpModa.Api.Ventas.Interfaces;
using ErpModa.Api.Ventas.Services;

namespace ErpModa.Api.Tests;

public class VentasServiceTests
{
    private static IVentasService CreateService() => new VentasService();

    private static CrearVentaDto VentaValida(string? metodoPago = "Efectivo") => new()
    {
        MetodoPago = metodoPago,
        Detalles = new List<DetalleCrearDto>
        {
            new() { VarianteId = 1, Cantidad = 2, PrecioUnitario = 25000m }
        }
    };

    private static CrearVentaDto VentaValida(int varianteId, int cantidad, decimal precioUnitario, string? metodoPago = "Efectivo") => new()
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
        var service = CreateService();

        var result = await service.CreateAsync(VentaValida());

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
        var service = CreateService();
        var created = await service.CreateAsync(VentaValida());

        var result = await service.GetByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("Pendiente", result.Estado);
    }

    [Fact]
    public async Task CrearVenta_ConListaDeDetallesVacia_LanzaArgumentException()
    {
        var service = CreateService();
        var dto = new CrearVentaDto { MetodoPago = "Efectivo", Detalles = new List<DetalleCrearDto>() };

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CrearVenta_CantidadInvalida_LanzaArgumentException(int cantidad)
    {
        var service = CreateService();
        var dto = VentaValida();
        dto.Detalles[0].Cantidad = cantidad;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task CrearVenta_PrecioInvalido_LanzaArgumentException(decimal precio)
    {
        var service = CreateService();
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
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(VentaValida(metodoPago)));
    }

    [Fact]
    public async Task ConfirmarVenta_EnPendiente_CambiaAConfirmada()
    {
        var service = CreateService();
        var created = await service.CreateAsync(VentaValida());

        var result = await service.ConfirmarAsync(created.Id);

        Assert.Equal("Confirmada", result.Estado);
    }

    [Fact]
    public async Task ConfirmarVenta_Inexistente_LanzaKeyNotFoundException()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ConfirmarAsync(999));
    }

    [Fact]
    public async Task ConfirmarVenta_YaConfirmada_LanzaInvalidOperationException()
    {
        var service = CreateService();
        var created = await service.CreateAsync(VentaValida());
        await service.ConfirmarAsync(created.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmarAsync(created.Id));
    }

    [Fact]
    public async Task AnularVenta_CambiaAAnulada_ConservaMotivoYNoElimina()
    {
        var service = CreateService();
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
        var service = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.AnularAsync(999, new AnularVentaDto { Motivo = "prueba" }));
    }

    [Fact]
    public async Task AnularVenta_YaAnulada_LanzaInvalidOperationException()
    {
        var service = CreateService();
        var created = await service.CreateAsync(VentaValida());
        await service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "primer motivo" });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "segundo motivo" }));
    }

    [Fact]
    public async Task AnularVenta_SinMotivo_LanzaArgumentException()
    {
        var service = CreateService();
        var created = await service.CreateAsync(VentaValida());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AnularAsync(created.Id, new AnularVentaDto { Motivo = "  " }));
    }

    [Fact]
    public async Task CrearVenta_ConVariosDetalles_CalculaSubtotalesYTotal()
    {
        var service = CreateService();
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
        var service = CreateService();
        await service.CreateAsync(VentaValida("Efectivo"));
        await service.CreateAsync(VentaValida("Tarjeta"));
        await service.CreateAsync(VentaValida("Transferencia"));

        var result = await service.GetAllAsync();

        Assert.Equal(3, result.Count());
    }

    [Fact]
    public async Task ObtenerVentas_FiltrandoPorEstado_DevuelveSoloEseEstado()
    {
        var service = CreateService();
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
        var service = CreateService();
        await service.CreateAsync(VentaValida());

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAllAsync(estado: estado));
    }

    [Fact]
    public async Task ObtenerVentas_EstadoVacio_SeTrataComoSinFiltro()
    {
        var service = CreateService();
        await service.CreateAsync(VentaValida());
        await service.CreateAsync(VentaValida());

        var result = await service.GetAllAsync(estado: "");

        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task ObtenerVentas_FiltrandoPorFecha_DevuelveVentasDelDia()
    {
        var service = CreateService();
        var creada = await service.CreateAsync(VentaValida());

        var result = await service.GetAllAsync(fecha: DateTime.UtcNow.Date);

        Assert.Contains(result, v => v.Id == creada.Id);
    }

    [Fact]
    public async Task ObtenerVentas_FiltrandoPorFechaSinVentas_DevuelveVacio()
    {
        var service = CreateService();
        await service.CreateAsync(VentaValida());

        var result = await service.GetAllAsync(fecha: new DateTime(2020, 1, 1));

        Assert.Empty(result);
    }

    [Fact]
    public async Task ObtenerVentas_FiltroCombinadoEstadoYFecha_DevuelveCoincidencias()
    {
        var service = CreateService();
        var confirmada = await service.CreateAsync(VentaValida("Efectivo"));
        await service.ConfirmarAsync(confirmada.Id);
        await service.CreateAsync(VentaValida("Tarjeta"));

        var result = await service.GetAllAsync(estado: "Confirmada", fecha: DateTime.UtcNow.Date);

        Assert.Single(result);
        Assert.Equal(confirmada.Id, result.First().Id);
    }

    [Fact]
    public async Task ObtenerResumen_ConVentasEnDistintosEstados_CalculaTotales()
    {
        var service = CreateService();
        var c1 = await service.CreateAsync(VentaValida("Efectivo"));
        await service.ConfirmarAsync(c1.Id);
        var c2 = await service.CreateAsync(VentaValida(1, 3, 10000m, "Tarjeta"));
        await service.ConfirmarAsync(c2.Id);
        var a = await service.CreateAsync(VentaValida(1, 1, 99999m, "Transferencia"));
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
        var service = CreateService();
        var confirmada = await service.CreateAsync(VentaValida("Efectivo"));
        await service.ConfirmarAsync(confirmada.Id);
        var anulada = await service.CreateAsync(VentaValida(1, 1, 99999m, "Tarjeta"));
        await service.AnularAsync(anulada.Id, new AnularVentaDto { Motivo = "registro erroneo" });

        var resumen = await service.GetResumenAsync();

        Assert.Equal(1, resumen.VentasAnuladas);
        Assert.Equal(50000m, resumen.TotalVentasConfirmadas);
        Assert.Equal(2, resumen.CantidadVentas);
    }
}