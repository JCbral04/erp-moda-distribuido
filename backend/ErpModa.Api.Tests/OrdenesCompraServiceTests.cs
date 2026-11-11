using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpModa.Api.Data;
using ErpModa.Api.Inventario.Models;
using ErpModa.Api.Proveedores.DTOs;
using ErpModa.Api.Proveedores.Interfaces;
using ErpModa.Api.Proveedores.Models;
using ErpModa.Api.Proveedores.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpModa.Api.Tests
{
    public class OrdenesCompraServiceTests : IDisposable
    {
        private readonly string _dbName;
        private readonly ErpModaDbContext _context;
        private readonly IOrdenesCompraService _service;
        private readonly int _proveedorId;
        private readonly int _productoId;

        public OrdenesCompraServiceTests()
        {
            _dbName = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<ErpModaDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;

            _context = new ErpModaDbContext(options);
            _service = new OrdenesCompraService(_context);

            _proveedorId = CrearProveedorAsync("900999888-1", "Proveedor Test", true).GetAwaiter().GetResult();
            _productoId = CrearProductoAsync("Tela Algodón 100%").GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private async Task<int> CrearProveedorAsync(string nit, string nombre, bool activo)
        {
            var proveedor = new Proveedor
            {
                Nombre = nombre,
                Nit = nit,
                Contacto = "Contacto",
                Activo = activo,
                CreadoEn = DateTime.UtcNow,
                ActualizadoEn = DateTime.UtcNow
            };
            _context.Proveedores.Add(proveedor);
            await _context.SaveChangesAsync();
            return proveedor.Id;
        }

        private async Task<int> CrearProductoAsync(string nombre)
        {
            var producto = new Producto
            {
                Nombre = nombre,
                Categoria = "Insumos",
                PrecioBase = 15000m,
                Activo = true,
                CreadoEn = DateTime.UtcNow,
                ActualizadoEn = DateTime.UtcNow
            };
            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();
            return producto.Id;
        }

        private CrearOrdenCompraDto OrdenValida(int? proveedorId = null, int? productoId = null) => new()
        {
            ProveedorId = proveedorId ?? _proveedorId,
            Observaciones = "Entrega urgente en bodega central",
            Detalles = new List<CrearDetalleOrdenDto>
            {
                new() { ProductoId = productoId ?? _productoId, Cantidad = 10, PrecioUnitario = 12000m }
            }
        };

        [Fact]
        public async Task CrearOrdenCompra_ConProveedorValidoYDetalles_CreaOrdenEnEstadoPendiente()
        {
            var dto = OrdenValida();

            var result = await _service.CreateAsync(dto);

            Assert.True(result.Id > 0);
            Assert.Equal(_proveedorId, result.ProveedorId);
            Assert.Equal("Proveedor Test", result.ProveedorNombre);
            Assert.Equal("Pendiente", result.Estado);
            Assert.Equal(120000m, result.Subtotal);
            Assert.Equal(0m, result.Impuesto);
            Assert.Equal(120000m, result.Total);
            Assert.Equal("Entrega urgente en bodega central", result.Observaciones);
            Assert.Single(result.Detalles);
            Assert.Equal(_productoId, result.Detalles[0].ProductoId);
            Assert.Equal(10, result.Detalles[0].Cantidad);
            Assert.Equal(12000m, result.Detalles[0].PrecioUnitario);
            Assert.Equal(120000m, result.Detalles[0].Subtotal);
        }

        [Fact]
        public async Task CrearOrdenCompra_ConProveedorInexistente_LanzaArgumentException()
        {
            const int idInexistente = 99999;
            var dto = OrdenValida(proveedorId: idInexistente);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
            Assert.Contains(idInexistente.ToString(), ex.Message);
        }

        [Fact]
        public async Task CrearOrdenCompra_ConProveedorInactivo_LanzaInvalidOperationException()
        {
            // RN-003: Un proveedor inactivo no puede recibir nuevas órdenes
            var inactivoId = await CrearProveedorAsync("900000999-9", "Proveedor Inactivo", false);
            var dto = OrdenValida(proveedorId: inactivoId);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(dto));
            Assert.Contains("inactivo", ex.Message);
        }

        [Fact]
        public async Task CrearOrdenCompra_SinDetalles_LanzaArgumentException()
        {
            var dto = new CrearOrdenCompraDto
            {
                ProveedorId = _proveedorId,
                Detalles = new List<CrearDetalleOrdenDto>()
            };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task CrearOrdenCompra_ConCantidadInvalida_LanzaArgumentException(int cantidad)
        {
            var dto = OrdenValida();
            dto.Detalles[0].Cantidad = cantidad;

            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-100)]
        public async Task CrearOrdenCompra_ConPrecioUnitarioInvalido_LanzaArgumentException(decimal precio)
        {
            var dto = OrdenValida();
            dto.Detalles[0].PrecioUnitario = precio;

            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
        }

        [Fact]
        public async Task CrearOrdenCompra_ConProductoInexistente_LanzaArgumentException()
        {
            const int productoInexistente = 88888;
            var dto = OrdenValida(productoId: productoInexistente);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
            Assert.Contains(productoInexistente.ToString(), ex.Message);
        }

        [Fact]
        public async Task CrearOrdenCompra_ConVariosDetalles_CalculaSubtotalesYTotalCorrectamente()
        {
            var p2 = await CrearProductoAsync("Botones Metálicos");
            var dto = new CrearOrdenCompraDto
            {
                ProveedorId = _proveedorId,
                Detalles = new List<CrearDetalleOrdenDto>
                {
                    new() { ProductoId = _productoId, Cantidad = 5, PrecioUnitario = 10000m },
                    new() { ProductoId = p2, Cantidad = 20, PrecioUnitario = 500m }
                }
            };

            var result = await _service.CreateAsync(dto);

            Assert.Equal(2, result.Detalles.Count);
            Assert.Equal(50000m, result.Detalles[0].Subtotal);
            Assert.Equal(10000m, result.Detalles[1].Subtotal);
            Assert.Equal(60000m, result.Total);
        }

        [Fact]
        public async Task ObtenerOrdenCompra_PorId_DevuelveOrdenConDetallesYProveedor()
        {
            var created = await _service.CreateAsync(OrdenValida());

            var result = await _service.GetByIdAsync(created.Id);

            Assert.NotNull(result);
            Assert.Equal(created.Id, result.Id);
            Assert.Equal("Proveedor Test", result.ProveedorNombre);
            Assert.Single(result.Detalles);
        }

        [Fact]
        public async Task ObtenerOrdenCompra_Inexistente_DevuelveNull()
        {
            var result = await _service.GetByIdAsync(9999);

            Assert.Null(result);
        }

        [Fact]
        public async Task ObtenerOrdenesCompra_FiltrandoPorProveedor_DevuelveSoloOrdenesDeEseProveedor()
        {
            var otroProveedorId = await CrearProveedorAsync("900888777-2", "Segundo Proveedor", true);

            var o1 = await _service.CreateAsync(OrdenValida(_proveedorId));
            var o2 = await _service.CreateAsync(OrdenValida(otroProveedorId));

            var ordenesP1 = await _service.GetAllAsync(proveedorId: _proveedorId);
            var ordenesP2 = await _service.GetAllAsync(proveedorId: otroProveedorId);

            Assert.Single(ordenesP1);
            Assert.Equal(o1.Id, ordenesP1.First().Id);

            Assert.Single(ordenesP2);
            Assert.Equal(o2.Id, ordenesP2.First().Id);
        }

        [Fact]
        public async Task ObtenerOrdenesCompra_FiltrandoPorEstado_DevuelveSoloOrdenesConEseEstado()
        {
            var o1 = await _service.CreateAsync(OrdenValida());

            var pendientes = await _service.GetAllAsync(estado: "Pendiente");
            var enviadas = await _service.GetAllAsync(estado: "Enviada");

            Assert.Single(pendientes);
            Assert.Equal(o1.Id, pendientes.First().Id);
            Assert.Empty(enviadas);
        }

        [Fact]
        public async Task ObtenerOrdenesCompra_ConEstadoInvalido_LanzaArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetAllAsync(estado: "EstadoInexistente"));
        }
    }
}

