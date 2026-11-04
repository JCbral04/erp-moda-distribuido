using ErpModa.Api.Data;
using ErpModa.Api.Proveedores.DTOs;
using ErpModa.Api.Proveedores.Interfaces;
using ErpModa.Api.Proveedores.Models;
using ErpModa.Api.Proveedores.Services;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Tests;

public class ProveedoresServiceTests : IDisposable
{
    private readonly string _dbName;
    private readonly ErpModaDbContext _context;
    private readonly IProveedoresService _service;

    public ProveedoresServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ErpModaDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _context = new ErpModaDbContext(options);
        _service = new ProveedoresService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
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

    private async Task<int> CrearProveedorAsync(string nit = "901234567-5")
    {
        var creado = await _service.CreateAsync(DtoValido(nit));
        return creado.Id;
    }

    [Fact]
    public async Task CrearProveedor_DatosValidos_CreaProveedorActivo()
    {
        var result = await _service.CreateAsync(DtoValido());

        Assert.True(result.Id > 0);
        Assert.Equal("Telas del Valle SAS", result.RazonSocial);
        Assert.Equal("901234567-5", result.Nit);
        Assert.Equal("contacto@telasdelvalle.com", result.Email);
        Assert.Equal("Activo", result.Estado);
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
        await _service.CreateAsync(DtoValido("901234567-5"));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(DtoValido("901234567-5")));

        Assert.Contains("Ya existe un proveedor con el NIT", ex.Message);
    }

    [Fact]
    public async Task ObtenerProveedor_PorId_DevuelveProveedorCreado()
    {
        var creado = await _service.CreateAsync(DtoValido());

        var result = await _service.GetByIdAsync(creado.Id);

        Assert.NotNull(result);
        Assert.Equal(creado.Id, result.Id);
        Assert.Equal("Telas del Valle SAS", result.RazonSocial);
        Assert.Equal("901234567-5", result.Nit);
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
        await _service.CreateAsync(DtoValido("111111111-1"));
        await _service.CreateAsync(DtoValido("222222222-2"));

        var result = (await _service.GetAllAsync()).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ActualizarProveedor_Existente_ActualizaCampos()
    {
        var creado = await _service.CreateAsync(DtoValido());

        var actualizado = await _service.UpdateAsync(creado.Id, new ActualizarProveedorDto
        {
            RazonSocial = "Telas del Valle S.A.S.",
            Telefono = "3009876543",
            Estado = "Inactivo"
        });

        Assert.NotNull(actualizado);
        Assert.Equal("Telas del Valle S.A.S.", actualizado.RazonSocial);
        Assert.Equal("3009876543", actualizado.Telefono);
        Assert.Equal("Inactivo", actualizado.Estado);
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
        var id = await CrearProveedorAsync();

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