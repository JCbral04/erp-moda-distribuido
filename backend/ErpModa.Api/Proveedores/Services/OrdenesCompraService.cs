using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ErpModa.Api.Data;
using ErpModa.Api.Proveedores.DTOs;
using ErpModa.Api.Proveedores.Interfaces;
using ErpModa.Api.Proveedores.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Proveedores.Services
{
    /// <summary>
    /// Implementación de los servicios de órdenes de compra sobre ErpModaDbContext.
    /// Incluye validación de existencia de proveedor (activo), existencia de productos,
    /// cálculos monetarios con precisión decimal y trazabilidad de auditoría.
    /// </summary>
    public class OrdenesCompraService : IOrdenesCompraService
    {
        private readonly ErpModaDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IAuditoriaLogService _auditoria;

        public OrdenesCompraService(
            ErpModaDbContext context,
            IHttpContextAccessor httpContextAccessor,
            IAuditoriaLogService auditoria)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _auditoria = auditoria ?? throw new ArgumentNullException(nameof(auditoria));
        }

        private string? UsuarioId =>
            _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true
                ? _httpContextAccessor.HttpContext.User.Identity.Name
                : null;

        private string? UsuarioNombre =>
            _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true
                ? _httpContextAccessor.HttpContext.User.Identity.Name
                : null;

        private string? DireccionIp =>
            _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

        public async Task<IEnumerable<OrdenCompraResponseDto>> GetAllAsync(int? proveedorId = null, string? estado = null)
        {
            var query = _context.OrdenesCompra
                .Include(o => o.Detalles)
                .Include(o => o.Proveedor)
                .AsQueryable();

            if (proveedorId.HasValue)
            {
                query = query.Where(o => o.ProveedorId == proveedorId.Value);
            }

            if (!string.IsNullOrWhiteSpace(estado))
            {
                if (!Enum.TryParse<EstadoOrdenCompra>(estado.Trim(), true, out var estadoParsed))
                {
                    throw new ArgumentException(
                        $"El estado '{estado}' no es válido. Valores válidos: {string.Join(", ", Enum.GetNames<EstadoOrdenCompra>())}.");
                }
                query = query.Where(o => o.EstadoStr == estadoParsed.ToString());
            }

            var ordenes = await query.OrderByDescending(o => o.Fecha).ThenByDescending(o => o.Id).ToListAsync();
            return ordenes.Select(MapToResponse);
        }

        public async Task<OrdenCompraResponseDto?> GetByIdAsync(int id)
        {
            var orden = await _context.OrdenesCompra
                .Include(o => o.Detalles)
                .Include(o => o.Proveedor)
                .FirstOrDefaultAsync(o => o.Id == id);

            return orden != null ? MapToResponse(orden) : null;
        }

        public async Task<OrdenCompraResponseDto> CreateAsync(CrearOrdenCompraDto crearDto)
        {
            if (crearDto == null)
                throw new ArgumentNullException(nameof(crearDto));

            if (crearDto.ProveedorId <= 0)
                throw new ArgumentException("El Id de proveedor es obligatorio.");

            var proveedor = await _context.Proveedores.FindAsync(crearDto.ProveedorId);
            if (proveedor == null)
                throw new ArgumentException($"El proveedor con Id {crearDto.ProveedorId} no existe.");

            // Regla de Negocio RN-003: Un proveedor inactivo no puede recibir nuevas órdenes.
            if (!proveedor.Activo)
                throw new InvalidOperationException(
                    $"No se pueden crear órdenes de compra para el proveedor '{proveedor.Nombre}' porque se encuentra inactivo.");

            if (crearDto.Detalles == null || !crearDto.Detalles.Any())
                throw new ArgumentException("La orden de compra debe contener al menos un detalle.");

            var detalleEntidades = new List<DetalleOrdenCompra>();
            foreach (var d in crearDto.Detalles)
            {
                if (d.ProductoId <= 0)
                    throw new ArgumentException("Cada detalle debe tener un Id de producto válido.");
                if (d.Cantidad <= 0)
                    throw new ArgumentException("La cantidad debe ser mayor a 0.");
                if (d.PrecioUnitario <= 0)
                    throw new ArgumentException("El precio unitario debe ser mayor a 0.");

                detalleEntidades.Add(new DetalleOrdenCompra
                {
                    ProductoId = d.ProductoId,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Subtotal = d.Cantidad * d.PrecioUnitario,
                    Version = Guid.NewGuid().ToByteArray()
                });
            }

            // Validar que los productos referenciados existan en Inventario.
            var productoIds = detalleEntidades.Select(d => d.ProductoId).Distinct().ToList();
            var productosExistentes = await _context.Productos
                .Where(p => productoIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync();

            var productoInexistente = productoIds.FirstOrDefault(id => !productosExistentes.Contains(id));
            if (productoInexistente != 0)
                throw new ArgumentException($"El producto con Id {productoInexistente} no existe.");

            var subtotal = detalleEntidades.Sum(d => d.Subtotal);
            var impuesto = 0m;
            var total = subtotal + impuesto;

            var orden = new OrdenCompra
            {
                ProveedorId = proveedor.Id,
                Fecha = DateTime.UtcNow,
                Estado = EstadoOrdenCompra.Pendiente,
                Subtotal = subtotal,
                Impuesto = impuesto,
                Total = total,
                Observaciones = string.IsNullOrWhiteSpace(crearDto.Observaciones)
                    ? null
                    : crearDto.Observaciones.Trim(),
                CreadoEn = DateTime.UtcNow,
                ActualizadoEn = DateTime.UtcNow,
                Version = Guid.NewGuid().ToByteArray(),
                Detalles = detalleEntidades,
                Proveedor = proveedor
            };

            // Transacción para garantizar consistencia si falla el guardado de algún detalle.
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _context.OrdenesCompra.Add(orden);
                await _context.SaveChangesAsync();

                var datosNuevos = JsonSerializer.Serialize(new
                {
                    ProveedorId = orden.ProveedorId,
                    Subtotal = orden.Subtotal,
                    Impuesto = orden.Impuesto,
                    Total = orden.Total,
                    Estado = orden.EstadoStr,
                    DetalleCount = orden.Detalles.Count
                });

                await _auditoria.RegistrarAsync(
                    UsuarioId ?? string.Empty, "OrdenCompra", orden.Id, "Crear",
                    null, datosNuevos, UsuarioNombre, DireccionIp);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return MapToResponse(orden);
        }

        private static OrdenCompraResponseDto MapToResponse(OrdenCompra orden)
        {
            return new OrdenCompraResponseDto
            {
                Id = orden.Id,
                ProveedorId = orden.ProveedorId,
                ProveedorNombre = orden.Proveedor?.Nombre ?? string.Empty,
                Fecha = orden.Fecha,
                Estado = orden.Estado.ToString(),
                Subtotal = orden.Subtotal,
                Impuesto = orden.Impuesto,
                Total = orden.Total,
                Observaciones = orden.Observaciones,
                Detalles = orden.Detalles.Select(d => new DetalleOrdenCompraDto
                {
                    Id = d.Id,
                    ProductoId = d.ProductoId,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Subtotal = d.Subtotal
                }).ToList(),
                CreadoEn = orden.CreadoEn
            };
        }
    }
}
