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
    /// <summary>
    /// Implementación de los servicios de proveedores sobre ErpModaDbContext.
    /// </summary>
    public class ProveedoresService : IProveedoresService
    {
        private readonly ErpModaDbContext _context;

        public ProveedoresService(ErpModaDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IEnumerable<ProveedorResponseDto>> GetAllAsync()
        {
            var proveedores = await _context.Proveedores
                .OrderBy(p => p.Id)
                .ToListAsync();

            return proveedores.Select(MapToResponse);
        }

        public async Task<ProveedorResponseDto?> GetByIdAsync(int id)
        {
            var proveedor = await _context.Proveedores.FirstOrDefaultAsync(p => p.Id == id);
            return proveedor != null ? MapToResponse(proveedor) : null;
        }

        public async Task<ProveedorResponseDto> CreateAsync(CrearProveedorDto crearDto)
        {
            var errores = crearDto.Validar();
            if (errores.Count > 0)
                throw new ArgumentException(string.Join(" ", errores));

            if (await _context.Proveedores.AnyAsync(p => p.Nit.Trim() == crearDto.Nit!.Trim()))
                throw new ArgumentException($"Ya existe un proveedor con el NIT '{crearDto.Nit}'.");

            var proveedor = new Proveedor
            {
                RazonSocial = crearDto.RazonSocial!.Trim(),
                Nit = crearDto.Nit!.Trim(),
                Contacto = string.IsNullOrWhiteSpace(crearDto.Contacto) ? null : crearDto.Contacto.Trim(),
                Email = crearDto.Email!.Trim(),
                Telefono = string.IsNullOrWhiteSpace(crearDto.Telefono) ? null : crearDto.Telefono.Trim(),
                Direccion = string.IsNullOrWhiteSpace(crearDto.Direccion) ? null : crearDto.Direccion.Trim(),
                FormaPago = string.IsNullOrWhiteSpace(crearDto.FormaPago) ? null : crearDto.FormaPago.Trim(),
                TiempoEntrega = crearDto.TiempoEntrega,
                Estado = EstadoProveedor.Activo
            };

            _context.Proveedores.Add(proveedor);
            await _context.SaveChangesAsync();

            return MapToResponse(proveedor);
        }

        public async Task<ProveedorResponseDto?> UpdateAsync(int id, ActualizarProveedorDto actualizarDto)
        {
            var proveedor = await _context.Proveedores.FirstOrDefaultAsync(p => p.Id == id);
            if (proveedor == null)
                return null;

            var errores = actualizarDto.Validar();
            if (errores.Count > 0)
                throw new ArgumentException(string.Join(" ", errores));

            if (actualizarDto.Nit != null &&
                await _context.Proveedores.AnyAsync(p => p.Id != id && p.Nit.Trim() == actualizarDto.Nit.Trim()))
                throw new ArgumentException($"Ya existe un proveedor con el NIT '{actualizarDto.Nit}'.");

            if (actualizarDto.RazonSocial != null)
                proveedor.RazonSocial = actualizarDto.RazonSocial.Trim();
            if (actualizarDto.Nit != null)
                proveedor.Nit = actualizarDto.Nit.Trim();
            if (actualizarDto.Contacto != null)
                proveedor.Contacto = string.IsNullOrWhiteSpace(actualizarDto.Contacto) ? null : actualizarDto.Contacto.Trim();
            if (actualizarDto.Email != null)
                proveedor.Email = actualizarDto.Email.Trim();
            if (actualizarDto.Telefono != null)
                proveedor.Telefono = string.IsNullOrWhiteSpace(actualizarDto.Telefono) ? null : actualizarDto.Telefono.Trim();
            if (actualizarDto.Direccion != null)
                proveedor.Direccion = string.IsNullOrWhiteSpace(actualizarDto.Direccion) ? null : actualizarDto.Direccion.Trim();
            if (actualizarDto.FormaPago != null)
                proveedor.FormaPago = string.IsNullOrWhiteSpace(actualizarDto.FormaPago) ? null : actualizarDto.FormaPago.Trim();
            if (actualizarDto.TiempoEntrega.HasValue)
                proveedor.TiempoEntrega = actualizarDto.TiempoEntrega.Value;
            if (actualizarDto.Estado != null)
                proveedor.Estado = Enum.Parse<EstadoProveedor>(actualizarDto.Estado.Trim(), ignoreCase: true);

            await _context.SaveChangesAsync();

            return MapToResponse(proveedor);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null)
                return false;

            _context.Proveedores.Remove(proveedor);
            await _context.SaveChangesAsync();
            return true;
        }

        private static ProveedorResponseDto MapToResponse(Proveedor proveedor)
        {
            return new ProveedorResponseDto
            {
                Id = proveedor.Id,
                RazonSocial = proveedor.RazonSocial,
                Nit = proveedor.Nit,
                Contacto = proveedor.Contacto,
                Email = proveedor.Email,
                Telefono = proveedor.Telefono,
                Direccion = proveedor.Direccion,
                FormaPago = proveedor.FormaPago,
                TiempoEntrega = proveedor.TiempoEntrega,
                Estado = proveedor.Estado.ToString()
            };
        }
    }
}