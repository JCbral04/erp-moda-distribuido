using ErpModa.Api.Proveedores.DTOs;

namespace ErpModa.Api.Tests;

public class ProveedoresDtosTests
{
    private static CrearProveedorDto ProveedorValido() => new()
    {
        RazonSocial = "Textiles Andinos S.A.S.",
        Nit = "901234567-5",
        Contacto = "María López",
        Email = "compras@textilesandinos.com",
        Telefono = "+57 1 2345678",
        Direccion = "Calle 10 # 20-30, Bogotá",
        FormaPago = "Crédito 30 días",
        TiempoEntrega = 15
    };

    [Fact]
    public void CrearProveedor_ConDatosValidos_NoDevuelveErrores()
    {
        var dto = ProveedorValido();

        var errores = dto.Validar();

        Assert.Empty(errores);
    }

    [Fact]
    public void CrearProveedor_SinRazonSocial_DevuelveError()
    {
        var dto = ProveedorValido();
        dto.RazonSocial = null;

        var errores = dto.Validar();

        Assert.Contains(errores, e => e.Contains("razón social", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CrearProveedor_SinNit_DevuelveError()
    {
        var dto = ProveedorValido();
        dto.Nit = "   ";

        var errores = dto.Validar();

        Assert.Contains(errores, e => e.Contains("NIT", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CrearProveedor_NitDemasiadoLargo_DevuelveError()
    {
        var dto = ProveedorValido();
        dto.Nit = new string('9', 21);

        Assert.Contains(dto.Validar(), e => e.Contains("20 caracteres"));
    }

    [Fact]
    public void CrearProveedor_NitConSimbolosInvalidos_DevuelveError()
    {
        var dto = ProveedorValido();
        dto.Nit = "ABC#123";

        Assert.Contains(dto.Validar(), e => e.Contains("solo puede contener"));
    }

    [Fact]
    public void CrearProveedor_EmailInvalido_DevuelveError()
    {
        var dto = ProveedorValido();
        dto.Email = "compras@sin punto";

        Assert.Contains(dto.Validar(), e => e.Contains("correo", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CrearProveedor_SinEmail_DevuelveError()
    {
        var dto = ProveedorValido();
        dto.Email = null;

        Assert.Contains(dto.Validar(), e => e.Contains("correo", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CrearProveedor_TelefonoConLetras_DevuelveError()
    {
        var dto = ProveedorValido();
        dto.Telefono = "ABC12345";

        var errores = dto.Validar();

        Assert.Single(errores);
        Assert.Contains(errores, e => e.Contains("teléfono", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CrearProveedor_TiempoEntregaCero_DevuelveError()
    {
        var dto = ProveedorValido();
        dto.TiempoEntrega = 0;

        var errores = dto.Validar();

        Assert.Single(errores);
        Assert.Contains(errores, e => e.Contains("tiempo de entrega", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ActualizarProveedor_DtoVacio_SinErrores()
    {
        var dto = new ActualizarProveedorDto();

        var errores = dto.Validar();

        Assert.Empty(errores);
    }

    [Fact]
    public void ActualizarProveedor_EmailProvistoInvalido_DevuelveError()
    {
        var dto = new ActualizarProveedorDto { Email = "no-es-correo" };

        var errores = dto.Validar();

        Assert.Single(errores);
        Assert.Contains(errores, e => e.Contains("correo", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ActualizarProveedor_EstadoValido_SinErrores()
    {
        var dto = new ActualizarProveedorDto { Estado = "inactivo" };

        Assert.Empty(dto.Validar());
    }

    [Fact]
    public void ActualizarProveedor_EstadoInvalido_DevuelveError()
    {
        var dto = new ActualizarProveedorDto { Estado = "suspendido" };

        var errores = dto.Validar();

        Assert.Single(errores);
        Assert.Contains(errores, e => e.Contains("estado", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ActualizarProveedor_NitProvistoValido_SinErrores()
    {
        var dto = new ActualizarProveedorDto { Nit = "800123456-1", Contacto = null };

        Assert.Empty(dto.Validar());
    }
}