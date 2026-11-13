using Xunit;
using ErpModa.Api.Proveedores.DTOs;
using ErpModa.Api.Proveedores.Models;
using ErpModa.Api.Proveedores.Services;
using ErpModa.Api.Proveedores.OrdenesCompra.DTOs;
using ErpModa.Api.Proveedores.OrdenesCompra.Models;
using ErpModa.Api.Proveedores.OrdenesCompra.Services;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Tests;

public class ConcurrenciaOptimistaTests : BasePostgreSQLTest
{
    [Fact]
    public async Task DosActualizacionesConMismaVersion_DebeLanzarConflicto()
    {
        // Paso 1: Crear proveedor
        var id = await CreateTestProveedorAsync("111111111-1");

        // Paso 2: Obtener la versión actual
        var proveedor = await _context.Proveedores.FindAsync(id);
        var versionOriginal = proveedor.Version;

        // Paso 3: Primera actualización exitosa
        var service = new ProveedoresService(_context);
        var updater1 = new ActualizarProveedorDto
        {
            RazonSocial = "Proveedor Actualizado",
            Version = versionOriginal
        };

        var actualizado = await service.UpdateAsync(id, updater1);
        Assert.NotNull(actualizado);
        Assert.Equal(versionOriginal + 1, actualizado.Version);

        // Paso 4: Segunda actualización con la misma versión original -> conflicto
        var updater2 = new ActualizarProveedorDto
        {
            RazonSocial = "Proveedor Modificado",
            Version = versionOriginal
        };

        var ex = await Assert.ThrowsAsync<ConcurrencyException>(() =>
            service.UpdateAsync(id, updater2));

        Assert.Contains("modificado por otro usuario", ex.Message);
    }

    [Fact]
    public async Task ActualizarOrden_VersionIncorrecta_LanzaConflicto()
    {
        // Paso 1: Crear proveedor y orden
        var proveedorId = await CreateTestProveedorAsync("222222222-2");
        var ordenId = await CreateTestOrdenCompraAsync(proveedorId);

        // Paso 2: Obtener la orden y su versión
        var orden = await _context.OrdenesCompra.Include(o => o.Detalles).FirstAsync(o => o.Id == ordenId);
        var versionOriginal = orden.Version;

        // Paso 3: Actualización exitosa
        var service = new OrdenesCompraService(_context);
        var updater1 = new ActualizarOrdenCompraDto
        {
            Observaciones = "Primera actualización",
            Estado = EstadoOrdenCompra.Enviada,
            Version = versionOriginal
        };

        var actualizado1 = await service.UpdateAsync(ordenId, updater1);
        Assert.NotNull(actualizado1);
        Assert.Equal(versionOriginal + 1, actualizado1.Version);

        // Paso 4: Segunda actualización con la misma versión original -> conflicto
        var updater2 = new ActualizarOrdenCompraDto
        {
            Observaciones = "Segunda actualización",
            Estado = EstadoOrdenCompra.Recibida,
            Version = versionOriginal
        };

        var ex = await Assert.ThrowsAsync<ConcurrencyException>(() =>
            service.UpdateAsync(ordenId, updater2));

        Assert.Contains("modificado por otro usuario", ex.Message);
    }

    [Fact]
    public async Task CrearOrdenConVersionInicialCorrecta()
    {
        var proveedorId = await CreateTestProveedorAsync("333333333-3");

        var orden = new OrdenCompra
        {
            ProveedorId = proveedorId,
            Fecha = DateTime.UtcNow,
            Estado = EstadoOrdenCompra.Pendiente,
            Subtotal = 0m,
            Total = 0m,
            Version = 1,
            CreadoEn = DateTime.UtcNow,
            ActualizadoEn = DateTime.UtcNow,
            Detalles = new List<DetalleOrdenCompra>
            {
                new()
                {
                    ProductoId = 1,
                    Cantidad = 1,
                    PrecioUnitario = 10000m,
                    Subtotal = 10000m,
                    Version = 1
                }
            }
        };

        _context.OrdenesCompra.Add(orden);
        await _context.SaveChangesAsync();

        var resultado = await _context.OrdenesCompra.FindAsync(orden.Id);
        Assert.NotNull(resultado);
        Assert.Equal(1, resultado.Version);
        Assert.Equal(10000m, resultado.Subtotal);
    }
}