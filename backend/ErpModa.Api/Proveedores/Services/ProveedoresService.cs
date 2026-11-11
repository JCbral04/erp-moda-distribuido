using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpModa.Api.Data;
using ErpModa.Api.Proveedores.DTOs;
using ErpModa.Api.Proveedores.Interfaces;
using ErpModa.Api.Proveedores.Models;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Proveedores.Services
{
    public class ProveedoresService : IProveedoresService
    {
        private readonly ErpModaDbContext _context;

        public ProveedoresService(ErpModaDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IEnumerable<ProveedorResponseDto>> GetAllAsync(bool? activo = null)
        {
            var query = _context.Proveedores.AsQueryable();

            if (activo.HasValue)
            {
                query = query.Where(p => p.Activo == activo.Value);
            }

            var proveedores = await query.ToListAsync();
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
            var existeNit = await _context.Proveedores.AnyAsync(p => p.Nit == nitTrimmed);
            if (existeNit)
                throw new ArgumentException($"Ya existe un proveedor registrado con el NIT '{nitTrimmed}'.");

            if (!string.IsNullOrWhiteSpace(crearDto.Correo))
            {
                var correoTrimmed = crearDto.Correo.Trim();
                if (!correoTrimmed.Contains('@') || !correoTrimmed.Contains('.'))
                    throw new ArgumentException("El correo electrónico no tiene un formato válido.");
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
                ActualizadoEn = DateTime.UtcNow
            };

            _context.Proveedores.Add(proveedor);
            await _context.SaveChangesAsync();

            return MapToResponse(proveedor);
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
                var existeNit = await _context.Proveedores.AnyAsync(p => p.Nit == nitTrimmed && p.Id != id);
                if (existeNit)
                    throw new ArgumentException($"Ya existe un proveedor registrado con el NIT '{nitTrimmed}'.");
            }

            if (!string.IsNullOrWhiteSpace(actualizarDto.Correo))
            {
                var correoTrimmed = actualizarDto.Correo.Trim();
                if (!correoTrimmed.Contains('@') || !correoTrimmed.Contains('.'))
                    throw new ArgumentException("El correo electrónico no tiene un formato válido.");
            }

            proveedor.Nombre = actualizarDto.Nombre.Trim();
            proveedor.Nit = nitTrimmed;
            proveedor.Contacto = actualizarDto.Contacto?.Trim();
            proveedor.Telefono = actualizarDto.Telefono?.Trim();
            proveedor.Correo = actualizarDto.Correo?.Trim();
            proveedor.Direccion = actualizarDto.Direccion?.Trim();
            proveedor.TiempoEntregaDias = Math.Max(0, actualizarDto.TiempoEntregaDias);
            proveedor.FormaPago = actualizarDto.FormaPago?.Trim();
            proveedor.Activo = actualizarDto.Activo;
            proveedor.ActualizadoEn = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MapToResponse(proveedor);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null)
                return false;

            var tieneOrdenes = await _context.OrdenesCompra.AnyAsync(o => o.ProveedorId == id);
            if (tieneOrdenes)
                throw new InvalidOperationException("No se puede eliminar el proveedor porque tiene órdenes de compra asociadas. Considere desactivarlo.");

            _context.Proveedores.Remove(proveedor);
            await _context.SaveChangesAsync();

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

