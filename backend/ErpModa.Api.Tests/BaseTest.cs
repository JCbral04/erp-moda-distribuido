using ErpModa.Api.Data;
using ErpModa.Api.Proveedores.Interfaces;
using ErpModa.Api.Proveedores.OrdenesCompra.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ErpModa.Api.Tests;

/// <summary>
/// Excepción lanzada cuando hay un conflicto de concurrencia optimista.
/// </summary>
public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
}

/// <summary>
/// Clase base para pruebas que usan PostgreSQL real (no InMemory).
/// Verifica la persistencia real y la concurrencia optimista.
/// </summary>
public abstract class BasePostgreSQLTest : IDisposable
{
    protected readonly ErpModaDbContext _context;
    protected readonly string _dbName;

    protected BasePostgreSQLTest()
    {
        _dbName = $"test_erp_moda_{Guid.NewGuid().ToString("N")}_{DateTime.UtcNow.Ticks}";

        var connectionString = System.Environment.GetEnvironmentVariable("PostgreSqlConnection__String");
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("No se encontró la variable de entorno 'PostgreSqlConnection__String'. Configurela para ejecutar pruebas con PostgreSQL.");
            var options = new DbContextOptionsBuilder<ErpModaDbContext>()
                .UseNpgsql(connectionString, o => o.MigrationsAssembly("ErpModa.Api"))
                .Options;

        _context = new ErpModaDbContext(options);
    }

    public virtual void Dispose()
    {
        try
        {
            _context.Database.EnsureDeleted();
        }
        catch
        {
            // Ignorar errores de eliminación en tests
        }
        _context.Dispose();
    }

    protected async Task<int> CreateTestProveedorAsync(string nit = "901234567-5", string razonSocial = "Test Proveedor")
    {
        var proveedor = new ErpModa.Api.Proveedores.Models.Proveedor
        {
            RazonSocial = razonSocial,
            Nit = nit,
            Email = "test@proveedor.com",
            Telefono = "3001234567",
            Direccion = "Calle 1 # 2-3",
            Estado = ErpModa.Api.Proveedores.Models.EstadoProveedor.Activo,
            Version = 0
        };

        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();
        return proveedor.Id;
    }

    protected async Task<int> CreateTestOrdenCompraAsync(int proveedorId, int productoId = 1, int cantidad = 2, decimal precioUnitario = 10000m)
    {
        var orden = new ErpModa.Api.Proveedores.OrdenesCompra.Models.OrdenCompra
        {
            ProveedorId = proveedorId,
            Fecha = DateTime.UtcNow,
            Estado = ErpModa.Api.Proveedores.OrdenesCompra.Models.EstadoOrdenCompra.Pendiente,
            Subtotal = cantidad * precioUnitario,
            Total = cantidad * precioUnitario,
            Version = 1,
            CreadoEn = DateTime.UtcNow,
            ActualizadoEn = DateTime.UtcNow,
            Detalles = new List<ErpModa.Api.Proveedores.OrdenesCompra.Models.DetalleOrdenCompra>
            {
                new()
                {
                    ProductoId = productoId,
                    Cantidad = cantidad,
                    PrecioUnitario = precioUnitario,
                    Subtotal = cantidad * precioUnitario,
                    Version = 1
                }
            }
        };

        _context.OrdenesCompra.Add(orden);
        await _context.SaveChangesAsync();
        return orden.Id;
    }

    protected IProveedoresService GetProveedoresService()
    {
        // El servicio se crea manualmente para tests
        return new ErpModa.Api.Proveedores.Services.ProveedoresService(_context);
    }

    protected IOrdenesCompraService GetOrdenesCompraService()
    {
        // El servicio se crea manualmente para tests
        return new ErpModa.Api.Proveedores.OrdenesCompra.Services.OrdenesCompraService(_context);
    }

    protected async Task<ErpModa.Api.Proveedores.Models.Proveedor> GetProveedorAsync(int id)
    {
        return await _context.Proveedores.FindAsync(id);
    }
}