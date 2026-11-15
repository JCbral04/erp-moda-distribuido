using ErpModa.Api.Data;
using ErpModa.Api.Proveedores.Interfaces;
using ErpModa.Api.Proveedores.OrdenesCompra.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

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
/// Cada instancia (es decir, cada prueba) crea una base de datos temporal
/// independiente, aplica todas las migraciones de EF Core y la elimina al finalizar.
/// </summary>
public abstract class BasePostgreSQLTest : IDisposable
{
    /// <summary>Variable de entorno con la cadena de conexión administrativa.</summary>
    private const string ConnectionEnvironmentVariable = "PostgreSqlConnection__String";

    /// <summary>Base de datos de mantenimiento sobre la que se ejecutan CREATE/DROP DATABASE.</summary>
    private const string MaintenanceDatabase = "postgres";

    /// <summary>Ensamblado que contiene las migraciones de EF Core.</summary>
    private const string MigrationsAssembly = "ErpModa.Api";

    /// <summary>
    /// Prefijo de las bases temporales. Solo minúsculas, dígitos y guion bajo,
    /// así que el nombre generado es un identificador válido sin necesidad de escapes.
    /// </summary>
    private const string DatabaseNamePrefix = "erp_moda_test_";

    protected readonly ErpModaDbContext _context;
    protected readonly string _dbName;

    private readonly string _adminConnectionString;

    /// <summary>Indica si esta instancia creó realmente la base temporal.</summary>
    private bool _databaseCreated;

    private bool _disposed;

    protected BasePostgreSQLTest()
    {
        _dbName = $"{DatabaseNamePrefix}{Guid.NewGuid():N}";

        var connectionString = System.Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"No se encontró la variable de entorno '{ConnectionEnvironmentVariable}'. Configurela para ejecutar pruebas con PostgreSQL.");

        _adminConnectionString = BuildConnectionString(connectionString, MaintenanceDatabase);

        var testConnectionString = BuildConnectionString(connectionString, _dbName, disablePooling: true);

        ErpModaDbContext? context = null;
        try
        {
            CreateDatabase();
            _databaseCreated = true;

            var options = new DbContextOptionsBuilder<ErpModaDbContext>()
                .UseNpgsql(testConnectionString, o => o.MigrationsAssembly(MigrationsAssembly))
                .Options;

            context = new ErpModaDbContext(options);
            context.Database.Migrate();
        }
        catch
        {
            try
            {
                context?.Dispose();
            }
            catch
            {
            }

            DeleteDatabaseQuietly();
            throw;
        }

        _context = context;
    }

    public virtual void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _context?.Dispose();
        }
        catch
        {
        }

        DeleteDatabaseQuietly();
    }

    private static string BuildConnectionString(string connectionString, string database, bool disablePooling = false)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = database
        };

        if (disablePooling)
        {
            builder.Pooling = false;
        }

        return builder.ConnectionString;
    }

    private void CreateDatabase()
    {
        using var connection = new NpgsqlConnection(_adminConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE {QuoteIdentifier(_dbName)}";
        command.ExecuteNonQuery();
    }

    private void DeleteDatabase()
    {
        using var connection = new NpgsqlConnection(_adminConnectionString);
        connection.Open();

        using (var terminate = connection.CreateCommand())
        {
            terminate.CommandText =
                "SELECT pg_terminate_backend(pid) " +
                "FROM pg_stat_activity " +
                "WHERE datname = CAST(@databaseName AS name) AND pid <> pg_backend_pid();";
            terminate.Parameters.AddWithValue("databaseName", _dbName);
            terminate.ExecuteNonQuery();
        }

        using var drop = connection.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS {QuoteIdentifier(_dbName)}";
        drop.ExecuteNonQuery();
    }

    private void DeleteDatabaseQuietly()
    {
        if (!_databaseCreated)
            return;

        _databaseCreated = false;

        try
        {
            DeleteDatabase();
        }
        catch
        {
        }
    }

    private static string QuoteIdentifier(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"") + "\"";

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
        return new ErpModa.Api.Proveedores.Services.ProveedoresService(_context);
    }

    protected IOrdenesCompraService GetOrdenesCompraService()
    {
        return new ErpModa.Api.Proveedores.OrdenesCompra.Services.OrdenesCompraService(_context);
    }

    protected async Task<ErpModa.Api.Proveedores.Models.Proveedor> GetProveedorAsync(int id)
    {
        return await _context.Proveedores.FindAsync(id);
    }
}