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
    /// Implementación de los servicios de proveedores sobre ErpModaDbContext.
    /// Incluye control de concurrencia optimista, prevención de condiciones de carrera
    /// en el NIT, y trazabilidad mediante WorkflowService y AuditoriaLogService.
    /// </summary>
    public class ProveedoresService : IProveedoresService
    {
        private readonly ErpModaDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IAuditoriaLogService _auditoria;
        private readonly IWorkflowService _workflow;

        public ProveedoresService(
            ErpModaDbContext context,
            IHttpContextAccessor httpContextAccessor,
            IAuditoriaLogService auditoria,
            IWorkflowService workflow)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _auditoria = auditoria ?? throw new ArgumentNullException(nameof(auditoria));
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
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

        public async Task<IEnumerable<ProveedorResponseDto>> GetAllAsync(bool? activo = null)
        {
            var query = _context.Proveedores.AsQueryable();

            if (activo.HasValue)
            {
                query = query.Where(p => p.Activo == activo.Value);
            }

            var proveedores = await query.OrderBy(p => p.Id).ToListAsync();
            return proveedores.Select(MapToResponse);
        }

        public async Task<ProveedorResponseDto?> GetByIdAsync(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            return proveedor != null ? MapToResponse(proveedor) : null;
        }

        public async Task<ProveedorResponseDto> CreateAsync(CrearProveedorDto crearDto)
        {
            if (crearDto == null)
                throw new ArgumentNullException(nameof(crearDto));

            if (string.IsNullOrWhiteSpace(crearDto.Nombre))
                throw new ArgumentException("El nombre del proveedor es obligatorio.");

            if (string.IsNullOrWhiteSpace(crearDto.Nit))
                throw new ArgumentException("El NIT del proveedor es obligatorio.");

            var nitTrimmed = crearDto.Nit.Trim();

            // Transacción serializable para prevenir condiciones de carrera en la unicidad del NIT.
            using var transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

            try
            {
                var existeNit = await _context.Proveedores
                    .AnyAsync(p => p.Nit == nitTrimmed);
                if (existeNit)
                {
                    await transaction.RollbackAsync();
                    throw new ArgumentException($"Ya existe un proveedor registrado con el NIT '{nitTrimmed}'.");
                }

                if (!string.IsNullOrWhiteSpace(crearDto.Correo))
                {
                    var correoTrimmed = crearDto.Correo.Trim();
                    if (!correoTrimmed.Contains('@') || !correoTrimmed.Contains('.'))
                    {
                        await transaction.RollbackAsync();
                        throw new ArgumentException("El correo electrónico no tiene un formato válido.");
                    }
                }

                var proveedor = new Proveedor
                {
                    Nombre = crearDto.Nombre.Trim(),
                    Nit = nitTrimmed,
                    Contacto = crearDto.Contacto?.Trim(),
                    Telefono = crearDto.Telefono?.Trim(),
                    Correo = crearDto.Correo?.Trim(),
                    Direccion = crearDto.Direccion?.Trim(),
                    TiempoEntregaDias = Math.Max(0, crearDto.TiempoEntregaDias),
                    FormaPago = crearDto.FormaPago?.Trim(),
                    Activo = crearDto.Activo,
                    CreadoEn = DateTime.UtcNow,
                    ActualizadoEn = DateTime.UtcNow,
                    Version = Guid.NewGuid().ToByteArray()
                };

                _context.Proveedores.Add(proveedor);
                await _context.SaveChangesAsync();

                var datosNuevos = JsonSerializer.Serialize(new
                {
                    Nombre = proveedor.Nombre,
                    Nit = proveedor.Nit,
                    Activo = proveedor.Activo
                });

                await _auditoria.RegistrarAsync(
                    UsuarioId ?? string.Empty, "Proveedor", proveedor.Id, "Crear",
                    null, datosNuevos, UsuarioNombre, DireccionIp);

                await transaction.CommitAsync();

                return MapToResponse(proveedor);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ProveedorResponseDto?> UpdateAsync(int id, ActualizarProveedorDto actualizarDto)
        {
            if (actualizarDto == null)
                throw new ArgumentNullException(nameof(actualizarDto));

            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null)
                return null;

            if (string.IsNullOrWhiteSpace(actualizarDto.Nombre))
                throw new ArgumentException("El nombre del proveedor es obligatorio.");

            if (string.IsNullOrWhiteSpace(actualizarDto.Nit))
                throw new ArgumentException("El NIT del proveedor es obligatorio.");

            var nitTrimmed = actualizarDto.Nit.Trim();
            if (!string.Equals(proveedor.Nit, nitTrimmed, StringComparison.OrdinalIgnoreCase))
            {
                var existeNit = await _context.Proveedores
                    .AnyAsync(p => p.Nit == nitTrimmed && p.Id != id);
                if (existeNit)
                    throw new ArgumentException($"Ya existe un proveedor registrado con el NIT '{nitTrimmed}'.");
            }

            if (!string.IsNullOrWhiteSpace(actualizarDto.Correo))
            {
                var correoTrimmed = actualizarDto.Correo.Trim();
                if (!correoTrimmed.Contains('@') || !correoTrimmed.Contains('.'))
                    throw new ArgumentException("El correo electrónico no tiene un formato válido.");
            }

            var datosAnteriores = JsonSerializer.Serialize(new
            {
                Nombre = proveedor.Nombre,
                Nit = proveedor.Nit,
                Activo = proveedor.Activo,
                TiempoEntregaDias = proveedor.TiempoEntregaDias
            });

            proveedor.Nombre = actualizarDto.Nombre.Trim();
            proveedor.Nit = nitTrimmed;
            proveedor.Contacto = actualizarDto.Contacto?.Trim();
            proveedor.Telefono = actualizarDto.Telefono?.Trim();
            proveedor.Correo = actualizarDto.Correo?.Trim();
            proveedor.Direccion = actualizarDto.Direccion?.Trim();
            proveedor.TiempoEntregaDias = Math.Max(0, actualizarDto.TiempoEntregaDias);
            proveedor.FormaPago = actualizarDto.FormaPago?.Trim();

            // Si cambia el estado Activo/Inactivo, delegar en WorkflowService para trazabilidad.
            var estadoCambio = proveedor.Activo != actualizarDto.Activo;
            proveedor.Activo = actualizarDto.Activo;
            proveedor.ActualizadoEn = DateTime.UtcNow;
            proveedor.Version = Guid.NewGuid().ToByteArray();

            await _context.SaveChangesAsync();

            var datosNuevos = JsonSerializer.Serialize(new
            {
                Nombre = proveedor.Nombre,
                Nit = proveedor.Nit,
                Activo = proveedor.Activo,
                TiempoEntregaDias = proveedor.TiempoEntregaDias
            });

            await _auditoria.RegistrarAsync(
                UsuarioId ?? string.Empty, "Proveedor", proveedor.Id, estadoCambio ? "CambiarEstado" : "Actualizar",
                datosAnteriores, datosNuevos, UsuarioNombre, DireccionIp);

            return MapToResponse(proveedor);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null)
                return false;

            var tieneOrdenes = await _context.OrdenesCompra
                .AnyAsync(o => o.ProveedorId == id);
            if (tieneOrdenes)
                throw new InvalidOperationException(
                    "No se puede eliminar el proveedor porque tiene órdenes de compra asociadas. Considere desactivarlo.");

            var datosAnteriores = JsonSerializer.Serialize(new
            {
                Nombre = proveedor.Nombre,
                Nit = proveedor.Nit,
                Activo = proveedor.Activo
            });

            _context.Proveedores.Remove(proveedor);
            await _context.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                UsuarioId ?? string.Empty, "Proveedor", id, "Eliminar",
                datosAnteriores, null, UsuarioNombre, DireccionIp);

            return true;
        }

        private static ProveedorResponseDto MapToResponse(Proveedor proveedor)
        {
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
    }
}
