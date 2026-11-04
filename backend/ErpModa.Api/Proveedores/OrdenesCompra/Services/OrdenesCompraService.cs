using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpModa.Api.Data;
using ErpModa.Api.Proveedores.OrdenesCompra.DTOs;
using ErpModa.Api.Proveedores.OrdenesCompra.Interfaces;
using ErpModa.Api.Proveedores.OrdenesCompra.Models;
using ErpModa.Api.Proveedores.Models;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Proveedores.OrdenesCompra.Services
{
    /// <summary>
    /// Implementación de los servicios de órdenes de compra sobre ErpModaDbContext.
    /// </summary>
    public class OrdenesCompraService : IOrdenesCompraService
    {
        private readonly ErpModaDbContext _context;

        public OrdenesCompraService(ErpModaDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IEnumerable<OrdenCompraResponseDto>> GetAllAsync()
        {
            var ordenes = await _context.OrdenesCompra
                .Include(o => o.Detalles)
                .OrderByDescending(o => o.Fecha)
                .ThenByDescending(o => o.Id)
                .ToListAsync();

            var proveedores = await _context.Proveedores
                .ToDictionaryAsync(p => p.Id);

            return ordenes.Select(o => MapToResponse(o, proveedores));
        }

        public async Task<OrdenCompraResponseDto?> GetByIdAsync(int id)
        {
            var orden = await _context.OrdenesCompra
                .Include(o => o.Detalles)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (orden == null)
                return null;

            var proveedores = await _context.Proveedores
                .ToDictionaryAsync(p => p.Id);

            return MapToResponse(orden, proveedores);
        }

        public async Task<OrdenCompraResponseDto> CreateAsync(CrearOrdenCompraDto crearDto)
        {
            var errores = crearDto.Validar();
            if (errores.Count > 0)
                throw new ArgumentException(string.Join(" ", errores));

            var proveedor = await _context.Proveedores.FindAsync(crearDto.ProveedorId);
            if (proveedor == null)
                throw new ArgumentException("El proveedor especificado no existe.");

            // RN-003: un proveedor inactivo no puede recibir nuevas órdenes.
            if (proveedor.Estado != EstadoProveedor.Activo)
                throw new ArgumentException(
                    $"No se puede crear una orden de compra para el proveedor '{proveedor.RazonSocial}' porque está inactivo.");

            var orden = new OrdenCompra
            {
                ProveedorId = crearDto.ProveedorId,
                Fecha = DateTime.UtcNow,
                Estado = EstadoOrdenCompra.Pendiente,
                Observaciones = string.IsNullOrWhiteSpace(crearDto.Observaciones)
                    ? null
                    : crearDto.Observaciones.Trim(),
                Detalles = crearDto.Detalles.Select(d => new DetalleOrdenCompra
                {
                    ProductoId = d.ProductoId,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Subtotal = d.Cantidad * d.PrecioUnitario
                }).ToList()
            };

            orden.Subtotal = orden.Detalles.Sum(d => d.Subtotal);
            orden.Total = orden.Subtotal;

            _context.OrdenesCompra.Add(orden);
            await _context.SaveChangesAsync();

            var proveedores = new Dictionary<int, Proveedor> { [crearDto.ProveedorId] = proveedor };
            var creado = await _context.OrdenesCompra
                .Include(o => o.Detalles)
                .FirstAsync(o => o.Id == orden.Id);

            return MapToResponse(creado, proveedores);
        }

        private static OrdenCompraResponseDto MapToResponse(OrdenCompra orden, Dictionary<int, Proveedor> proveedores)
        {
            var proveedorRazonSocial = proveedores.TryGetValue(orden.ProveedorId, out var p)
                ? p.RazonSocial
                : null;

            return new OrdenCompraResponseDto
            {
                Id = orden.Id,
                ProveedorId = orden.ProveedorId,
                ProveedorRazonSocial = proveedorRazonSocial,
                Fecha = orden.Fecha,
                Estado = orden.Estado.ToString(),
                Subtotal = orden.Subtotal,
                Total = orden.Total,
                Observaciones = orden.Observaciones,
                Detalles = orden.Detalles.Select(d => new DetalleOrdenCompraDto
                {
                    Id = d.Id,
                    ProductoId = d.ProductoId,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Subtotal = d.Subtotal
                }).ToList()
            };
        }
    }
}