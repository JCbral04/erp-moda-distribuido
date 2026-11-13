using Xunit;
using ErpModa.Api.Proveedores.DTOs;
using ErpModa.Api.Proveedores.Models;
using ErpModa.Api.Proveedores.Services;
using ErpModa.Api.Proveedores.OrdenesCompra.DTOs;
using ErpModa.Api.Proveedores.OrdenesCompra.Interfaces;
using ErpModa.Api.Proveedores.OrdenesCompra.Services;

namespace ErpModa.Api.Tests;

public class OrdenesCompraServiceTests : BasePostgreSQLTest
{
    private readonly IOrdenesCompraService _service;
    private readonly ProveedoresService _proveedoresService;

    public OrdenesCompraServiceTests()
    {
        _proveedoresService = new ProveedoresService(_context);
        _service = GetOrdenesCompraService();
    }

    private async Task<int> CrearProveedorActivoAsync(string nit = "901234567-5")
    {
        var creado = await _proveedoresService.CreateAsync(new CrearProveedorDto
        {
            RazonSocial = "Telas del Valle SAS",
            Nit = nit,
            Email = "contacto@telasdelvalle.com",
            TiempoEntrega = 7
        });
        return creado.Id;
    }

    private async Task<int> CrearProveedorInactivoAsync()
    {
        var creado = await _proveedoresService.CreateAsync(new CrearProveedorDto
        {
            RazonSocial = "Tela Inactiva SAS",
            Nit = "888888888-8",
            Email = "inactiva@telas.com",
            TiempoEntrega = 5
        });
        await _proveedoresService.UpdateAsync(creado.Id, new ActualizarProveedorDto { Estado = "Inactivo" });
        return creado.Id;
    }

    private CrearOrdenCompraDto OrdenValida(int proveedorId) => new()
    {
        ProveedorId = proveedorId,
        Observaciones = "Entrega en bodega principal",
        Detalles = new List<DetalleOrdenCompraCrearDto>
        {
            new() { ProductoId = 101, Cantidad = 2, PrecioUnitario = 25000m },
            new() { ProductoId = 102, Cantidad = 5, PrecioUnitario = 12000m }
        }
    };

    [Fact]
    public async Task CrearOrdenCompra_ProveedorValido_CreaOrdenConTotales()
    {
        var proveedorId = await CrearProveedorActivoAsync();
        var result = await _service.CreateAsync(OrdenValida(proveedorId));

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(proveedorId, result.ProveedorId);
        Assert.Equal("Telas del Valle SAS", result.ProveedorRazonSocial);
        Assert.Equal("Pendiente", result.Estado);
        Assert.Equal(2, result.Detalles.Count);
        Assert.Equal(110000m, result.Subtotal);
        Assert.Equal(110000m, result.Total);
        Assert.Equal(50000m, result.Detalles[0].Subtotal);
        Assert.Equal(60000m, result.Detalles[1].Subtotal);
        Assert.Equal(1, result.Version);
    }

    [Fact]
    public async Task CrearOrdenCompra_ProveedorInexistente_LanzaArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(OrdenValida(999)));

        Assert.Contains("no existe", ex.Message);
    }

    [Fact]
    public async Task CrearOrdenCompra_ProveedorInactivo_LanzaArgumentException()
    {
        var proveedorId = await CrearProveedorInactivoAsync();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(OrdenValida(proveedorId)));

        Assert.Contains("inactivo", ex.Message);
    }

    [Fact]
    public async Task CrearOrdenCompra_SinDetalles_LanzaArgumentException()
    {
        var proveedorId = await CrearProveedorActivoAsync();
        var dto = OrdenValida(proveedorId);
        dto.Detalles.Clear();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(dto));

        Assert.Contains("al menos un producto", ex.Message);
    }

    [Fact]
    public async Task ObtenerOrden_PorId_DevuelveOrdenConDetalles()
    {
        var proveedorId = await CrearProveedorActivoAsync();
        var creado = await _service.CreateAsync(OrdenValida(proveedorId));
        var result = await _service.GetByIdAsync(creado.Id);

        Assert.NotNull(result);
        Assert.Equal(creado.Id, result.Id);
        Assert.Equal("Telas del Valle SAS", result.ProveedorRazonSocial);
        Assert.Equal(2, result.Detalles.Count);
    }

    [Fact]
    public async Task ObtenerOrden_IdInexistente_DevuelveNull()
    {
        var result = await _service.GetByIdAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task ListarOrdenes_DevuelveTodas()
    {
        var proveedorId = await CrearProveedorActivoAsync();
        await _service.CreateAsync(OrdenValida(proveedorId));
        await _service.CreateAsync(OrdenValida(proveedorId));

        var result = (await _service.GetAllAsync()).ToList();
        Assert.Equal(2, result.Count);
    }
}