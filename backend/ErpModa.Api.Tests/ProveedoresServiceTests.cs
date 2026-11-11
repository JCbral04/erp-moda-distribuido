using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpModa.Api.Data;
using ErpModa.Api.Proveedores.DTOs;
using ErpModa.Api.Proveedores.Interfaces;
using ErpModa.Api.Proveedores.Models;
using ErpModa.Api.Proveedores.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpModa.Api.Tests
{
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

        private static CrearProveedorDto ProveedorValido(string nit = "900123456-1", string nombre = "Textiles de Colombia S.A.S.")
        {
            return new CrearProveedorDto
            {
                Nombre = nombre,
                Nit = nit,
                Contacto = "Carlos Gómez",
                Telefono = "3001234567",
                Correo = "contacto@textilescolombia.com",
                Direccion = "Calle 100 # 15-20, Bogotá",
                TiempoEntregaDias = 15,
                FormaPago = "Crédito 30 días",
                Activo = true
            };
        }

        [Fact]
        public async Task CrearProveedor_ConDatosValidos_CreaProveedorExitosamente()
        {
            var dto = ProveedorValido();

            var result = await _service.CreateAsync(dto);

            Assert.True(result.Id > 0);
            Assert.Equal(dto.Nombre, result.Nombre);
            Assert.Equal(dto.Nit, result.Nit);
            Assert.Equal(dto.Contacto, result.Contacto);
            Assert.Equal(dto.Telefono, result.Telefono);
            Assert.Equal(dto.Correo, result.Correo);
            Assert.Equal(dto.Direccion, result.Direccion);
            Assert.Equal(15, result.TiempoEntregaDias);
            Assert.Equal("Crédito 30 días", result.FormaPago);
            Assert.True(result.Activo);
            Assert.NotEqual(default, result.CreadoEn);
            Assert.NotEqual(default, result.ActualizadoEn);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task CrearProveedor_SinNombre_LanzaArgumentException(string? nombre)
        {
            var dto = ProveedorValido();
            dto.Nombre = nombre!;

            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task CrearProveedor_SinNit_LanzaArgumentException(string? nit)
        {
            var dto = ProveedorValido();
            dto.Nit = nit!;

            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
        }

        [Fact]
        public async Task CrearProveedor_ConNitDuplicado_LanzaArgumentException()
        {
            var dto1 = ProveedorValido("900111222-3", "Proveedor A");
            await _service.CreateAsync(dto1);

            var dto2 = ProveedorValido("900111222-3", "Proveedor B");
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto2));
            Assert.Contains("900111222-3", ex.Message);
        }

        [Theory]
        [InlineData("correoinvalido")]
        [InlineData("correo@")]
        [InlineData("correo@sin-punto")]
        public async Task CrearProveedor_ConCorreoInvalido_LanzaArgumentException(string correo)
        {
            var dto = ProveedorValido();
            dto.Correo = correo;

            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
        }

        [Fact]
        public async Task ObtenerProveedor_PorId_DevuelveProveedorCorrecto()
        {
            var created = await _service.CreateAsync(ProveedorValido());

            var result = await _service.GetByIdAsync(created.Id);

            Assert.NotNull(result);
            Assert.Equal(created.Id, result.Id);
            Assert.Equal(created.Nit, result.Nit);
        }

        [Fact]
        public async Task ObtenerProveedor_Inexistente_DevuelveNull()
        {
            var result = await _service.GetByIdAsync(9999);

            Assert.Null(result);
        }

        [Fact]
        public async Task ObtenerProveedores_SinFiltro_DevuelveTodos()
        {
            await _service.CreateAsync(ProveedorValido("900000001", "P1"));
            await _service.CreateAsync(ProveedorValido("900000002", "P2"));

            var result = await _service.GetAllAsync();

            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task ObtenerProveedores_FiltrandoPorActivo_DevuelveProveedoresFiltrados()
        {
            var p1 = ProveedorValido("900000001", "P1");
            p1.Activo = true;
            await _service.CreateAsync(p1);

            var p2 = ProveedorValido("900000002", "P2");
            p2.Activo = false;
            await _service.CreateAsync(p2);

            var activos = await _service.GetAllAsync(activo: true);
            var inactivos = await _service.GetAllAsync(activo: false);

            Assert.Single(activos);
            Assert.Single(inactivos);
            Assert.Equal("P1", activos.First().Nombre);
            Assert.Equal("P2", inactivos.First().Nombre);
        }

        [Fact]
        public async Task ActualizarProveedor_ConDatosValidos_ActualizaCorrectamente()
        {
            var created = await _service.CreateAsync(ProveedorValido());
            var updateDto = new ActualizarProveedorDto
            {
                Nombre = "Textiles Renovados S.A.S.",
                Nit = created.Nit,
                Contacto = "Nuevo Contacto",
                Telefono = "3119876543",
                Correo = "nuevo@correo.com",
                Direccion = "Nueva dirección",
                TiempoEntregaDias = 20,
                FormaPago = "Contado",
                Activo = false
            };

            var updated = await _service.UpdateAsync(created.Id, updateDto);

            Assert.NotNull(updated);
            Assert.Equal("Textiles Renovados S.A.S.", updated.Nombre);
            Assert.Equal("Nuevo Contacto", updated.Contacto);
            Assert.False(updated.Activo);
            Assert.Equal(20, updated.TiempoEntregaDias);
            Assert.Equal("Contado", updated.FormaPago);
        }

        [Fact]
        public async Task ActualizarProveedor_Inexistente_DevuelveNull()
        {
            var updateDto = new ActualizarProveedorDto
            {
                Nombre = "Inexistente",
                Nit = "123456789"
            };

            var updated = await _service.UpdateAsync(9999, updateDto);

            Assert.Null(updated);
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData(null)]
        public async Task ActualizarProveedor_SinNombre_LanzaArgumentException(string? nombre)
        {
            var created = await _service.CreateAsync(ProveedorValido());
            var updateDto = new ActualizarProveedorDto
            {
                Nombre = nombre!,
                Nit = created.Nit
            };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(created.Id, updateDto));
        }

        [Fact]
        public async Task ActualizarProveedor_ConNitDuplicado_LanzaArgumentException()
        {
            await _service.CreateAsync(ProveedorValido("900000001", "P1"));
            var p2 = await _service.CreateAsync(ProveedorValido("900000002", "P2"));

            var updateDto = new ActualizarProveedorDto
            {
                Nombre = "P2 Actualizado",
                Nit = "900000001" // Ya tomado por P1
            };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(p2.Id, updateDto));
        }

        [Fact]
        public async Task ActualizarProveedor_ConMismoNit_PermiteActualizar()
        {
            var p = await _service.CreateAsync(ProveedorValido("900000001", "P1"));
            var updateDto = new ActualizarProveedorDto
            {
                Nombre = "P1 Nuevo Nombre",
                Nit = "900000001" // Mismo NIT del mismo proveedor
            };

            var updated = await _service.UpdateAsync(p.Id, updateDto);

            Assert.NotNull(updated);
            Assert.Equal("P1 Nuevo Nombre", updated.Nombre);
        }

        [Fact]
        public async Task EliminarProveedor_SinOrdenes_EliminaExitosamente()
        {
            var p = await _service.CreateAsync(ProveedorValido());

            var deleted = await _service.DeleteAsync(p.Id);

            Assert.True(deleted);
            var retrieved = await _service.GetByIdAsync(p.Id);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task EliminarProveedor_Inexistente_DevuelveFalse()
        {
            var deleted = await _service.DeleteAsync(9999);

            Assert.False(deleted);
        }

        [Fact]
        public async Task EliminarProveedor_ConOrdenesAsociadas_LanzaInvalidOperationException()
        {
            var p = await _service.CreateAsync(ProveedorValido());

            // Agregar una orden de compra asociada
            _context.OrdenesCompra.Add(new OrdenCompra
            {
                ProveedorId = p.Id,
                Fecha = DateTime.UtcNow,
                Estado = EstadoOrdenCompra.Pendiente,
                Subtotal = 10000,
                Impuesto = 0,
                Total = 10000,
                CreadoEn = DateTime.UtcNow,
                ActualizadoEn = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteAsync(p.Id));
        }

        [Fact]
        public void ModeloEfCore_OrdenCompra_ConfiguradoCorrectamenteSinSombras()
        {
            var entityType = _context.Model.FindEntityType(typeof(OrdenCompra));
            Assert.NotNull(entityType);

            Assert.Null(entityType!.FindProperty("ProveedorId1"));

            var fkHaciaProveedor = entityType.GetForeignKeys()
                .Where(fk => fk.PrincipalEntityType.ClrType == typeof(Proveedor))
                .ToList();
            Assert.Single(fkHaciaProveedor);
            Assert.Equal("ProveedorId", fkHaciaProveedor[0].Properties[0].Name);
        }

        [Fact]
        public void ModeloEfCore_DetalleOrdenCompra_ConfiguradoCorrectamenteSinSombras()
        {
            var entityType = _context.Model.FindEntityType(typeof(DetalleOrdenCompra));
            Assert.NotNull(entityType);

            Assert.Null(entityType!.FindProperty("OrdenCompraId1"));

            var fkHaciaOrden = entityType.GetForeignKeys()
                .Where(fk => fk.PrincipalEntityType.ClrType == typeof(OrdenCompra))
                .ToList();
            Assert.Single(fkHaciaOrden);
            Assert.Equal("OrdenCompraId", fkHaciaOrden[0].Properties[0].Name);
        }
    }
}
