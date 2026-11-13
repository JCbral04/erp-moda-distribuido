using Xunit;
using ErpModa.Api.Proveedores.Interfaces;
using ErpModa.Api.Proveedores.DTOs;
using ErpModa.Api.Proveedores.Models;
using ErpModa.Api.Proveedores.Services;
using ErpModa.Api.Proveedores.OrdenesCompra.DTOs;
using ErpModa.Api.Proveedores.OrdenesCompra.Interfaces;
using ErpModa.Api.Proveedores.OrdenesCompra.Models;
using ErpModa.Api.Proveedores.OrdenesCompra.Services;

namespace ErpModa.Api.Tests;

public class ProveedoresServiceTests : BasePostgreSQLTest
{
    private readonly IProveedoresService _service;

    public ProveedoresServiceTests()
    {
        _service = GetProveedoresService();
    }

    private CrearProveedorDto DtoValido(string nit = "901234567-5") => new()
    {
        RazonSocial = "Telas del Valle SAS",
        Nit = nit,
        Contacto = "Juan Pérez",
        Email = "contacto@telasdelvalle.com",
        Telefono = "3001234567",
        Direccion = "Calle 10 # 20-30",
        FormaPago = "30 días",
        TiempoEntrega = 7
    };

    [Fact]
    public async Task CrearProveedor_DatosValidos_CreaProveedorActivo()
    {
        var result = await _service.CreateAsync(DtoValido());

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("Telas del Valle SAS", result.RazonSocial);
        Assert.Equal("901234567-5", result.Nit);
        Assert.Equal("contacto@telasdelvalle.com", result.Email);
        Assert.Equal("Activo", result.Estado);
        Assert.Equal(0, result.Version);
    }

    [Fact]
    public async Task CrearProveedor_DtoInvalido_LanzaArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CrearProveedorDto()));

        Assert.Contains("obligatoria", ex.Message);
    }

    [Fact]
    public async Task CrearProveedor_NitDuplicado_LanzaArgumentException()
    {
        await CreateTestProveedorAsync("901234567-5");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(DtoValido("901234567-5")));

        Assert.Contains("Ya existe un proveedor con el NIT", ex.Message);
    }

    [Fact]
    public async Task ObtenerProveedor_PorId_DevuelveProveedorCreado()
    {
        var id = await CreateTestProveedorAsync();
        var result = await _service.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal(id, result.Id);
        Assert.Equal("Telas del Valle SAS", result.RazonSocial);
    }

    [Fact]
    public async Task ObtenerProveedor_IdInexistente_DevuelveNull()
    {
        var result = await _service.GetByIdAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task ListarProveedores_DevuelveTodosRegistrados()
    {
        await CreateTestProveedorAsync("111111111-1");
        await CreateTestProveedorAsync("222222222-2");

        var result = (await _service.GetAllAsync()).ToList();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ActualizarProveedor_Existente_ActualizaCampos()
    {
        var id = await CreateTestProveedorAsync();
        var updater = new ActualizarProveedorDto
        {
            RazonSocial = "Telas del Valle S.A.S.",
            Telefono = "3009876543",
            Estado = "Inactivo",
            Version = 0
        };

        var updated = await _service.UpdateAsync(id, updater);

        Assert.NotNull(updated);
        Assert.Equal("Telas del Valle S.A.S.", updated.RazonSocial);
        Assert.Equal("3009876543", updated.Telefono);
        Assert.Equal("Inactivo", updated.Estado);
        Assert.Equal(1, updated.Version);
    }

    [Fact]
    public async Task ActualizarProveedor_VersionIncorrecta_LanzaExcepcion()
    {
        var id = await CreateTestProveedorAsync();

        var updater = new ActualizarProveedorDto
        {
            RazonSocial = "Otro nombre",
            Version = 999
        };

        var ex = await Assert.ThrowsAsync<ConcurrencyException>(() =>
            _service.UpdateAsync(id, updater));

        Assert.Contains("modificado por otro usuario", ex.Message);
    }

    [Fact]
    public async Task ActualizarProveedor_Inexistente_DevuelveNull()
    {
        var result = await _service.UpdateAsync(999, new ActualizarProveedorDto { RazonSocial = "X" });
        Assert.Null(result);
    }

    [Fact]
    public async Task EliminarProveedor_Existente_DevuelveTrue()
    {
        var id = await CreateTestProveedorAsync();
        var deleted = await _service.DeleteAsync(id);

        Assert.True(deleted);
        Assert.Null(await _service.GetByIdAsync(id));
    }

    [Fact]
    public async Task EliminarProveedor_Inexistente_DevuelveFalse()
    {
        var deleted = await _service.DeleteAsync(999);
        Assert.False(deleted);
    }
}