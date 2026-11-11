using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ErpModa.Api.Data;
using ErpModa.Api.Proveedores.DTOs;
using ErpModa.Api.Proveedores.Interfaces;
using ErpModa.Api.Proveedores.Models;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Proveedores.Services
{
    /// <summary>
    /// Implementación del servicio de Workflow para cambios de estado.
    /// Centraliza todas las transiciones de estado del módulo Proveedores
    /// y garantiza que cada cambio quede registrado en la bitácora de auditoría.
    /// </summary>
    public class WorkflowService : IWorkflowService
    {
        private readonly ErpModaDbContext _context;
        private readonly IAuditoriaLogService _auditoria;

        public WorkflowService(ErpModaDbContext context, IAuditoriaLogService auditoria)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _auditoria = auditoria ?? throw new ArgumentNullException(nameof(auditoria));
        }

        public async Task<ProveedorResponseDto> CambiarEstadoProveedorAsync(int proveedorId, string nuevoEstado,
            string usuarioId, string? usuarioNombre = null, string? direccionIp = null)
        {
            var proveedor = await _context.Proveedores.FindAsync(proveedorId);
            if (proveedor == null)
                throw new KeyNotFoundException($"El proveedor con Id {proveedorId} no existe.");

            var estadoAnterior = proveedor.Activo ? "Activo" : "Inactivo";
            var nuevoEstadoTrim = (nuevoEstado ?? string.Empty).Trim();

            bool nuevoActivo;
            if (string.Equals(nuevoEstadoTrim, "Activo", StringComparison.OrdinalIgnoreCase))
                nuevoActivo = true;
            else if (string.Equals(nuevoEstadoTrim, "Inactivo", StringComparison.OrdinalIgnoreCase))
                nuevoActivo = false;
            else
                throw new ArgumentException($"El estado '{nuevoEstado}' no es válido. Valores válidos: Activo, Inactivo.");

            if (estadoAnterior.Equals(nuevoActivo ? "Activo" : "Inactivo", StringComparison.OrdinalIgnoreCase))
            {
                // No hay cambio real, retornar el proveedor actual
                return new ProveedorResponseDto
                {
                    Id = proveedor.Id,
                    Nombre = proveedor.Nombre,
                    Nit = proveedor.Nit,
                    Activo = proveedor.Activo,
                    CreadoEn = proveedor.CreadoEn,
                    ActualizadoEn = proveedor.ActualizadoEn
                };
            }

            // Regla de negocio RN-003: un proveedor inactivo no puede recibir nuevas órdenes.
            // El cambio de estado a Inactivo se registra; las órdenes existentes se respetan.
            var datosAnteriores = JsonSerializer.Serialize(new { Activo = proveedor.Activo });

            proveedor.Activo = nuevoActivo;
            proveedor.ActualizadoEn = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var datosNuevos = JsonSerializer.Serialize(new { Activo = proveedor.Activo });

            await _auditoria.RegistrarAsync(
                usuarioId, "Proveedor", proveedor.Id, "CambiarEstado",
                datosAnteriores, datosNuevos, usuarioNombre, direccionIp);

            return new ProveedorResponseDto
            {
                Id = proveedor.Id,
                Nombre = proveedor.Nombre,
                Nit = proveedor.Nit,
                Contacto = proveedor.Contacto,
                Telefono = proveedor.Telefono,
                Correo = proveedor.Correo,
                Direccion = proveedor.Direccion,
                TiempoEntregaDias = proveedor.TiempoEntregaDias,
                FormaPago = proveedor.FormaPago,
                Activo = proveedor.Activo,
                CreadoEn = proveedor.CreadoEn,
                ActualizadoEn = proveedor.ActualizadoEn
            };
        }

        public async Task<OrdenCompraResponseDto> CambiarEstadoOrdenAsync(int ordenId, string nuevoEstado,
            string usuarioId, string? usuarioNombre = null, string? direccionIp = null)
        {
            var orden = await _context.OrdenesCompra
                .Include(o => o.Detalles)
                .Include(o => o.Proveedor)
                .FirstOrDefaultAsync(o => o.Id == ordenId);

            if (orden == null)
                throw new KeyNotFoundException($"La orden de compra con Id {ordenId} no existe.");

            var estadoAnterior = orden.EstadoStr;

            if (!Enum.TryParse<EstadoOrdenCompra>(nuevoEstado.Trim(), true, out var estadoParsed))
            {
                throw new ArgumentException(
                    $"El estado '{nuevoEstado}' no es válido. Valores válidos: {string.Join(", ", Enum.GetNames<EstadoOrdenCompra>())}.");
            }

            if (orden.EstadoStr.Equals(estadoParsed.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return MapToResponse(orden);
            }

            var datosAnteriores = JsonSerializer.Serialize(new { Estado = orden.EstadoStr });

            orden.EstadoStr = estadoParsed.ToString();
            orden.ActualizadoEn = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var datosNuevos = JsonSerializer.Serialize(new { Estado = orden.EstadoStr });

            await _auditoria.RegistrarAsync(
                usuarioId, "OrdenCompra", orden.Id, "CambiarEstado",
                datosAnteriores, datosNuevos, usuarioNombre, direccionIp);

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
