using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErpModa.Api.Ventas.DTOs;
using ErpModa.Api.Ventas.Interfaces;
using ErpModa.Api.Ventas.Models;

namespace ErpModa.Api.Ventas.Services
{
    public class VentasService : IVentasService
    {
        private readonly List<Venta> _ventas = new();
        private int _nextId = 1;
        private int _nextDetalleId = 1;

        public Task<IEnumerable<VentaResponseDto>> GetAllAsync()
        {
            var result = _ventas.Select(MapToResponse);
            return Task.FromResult(result);
        }

        public Task<VentaResponseDto?> GetByIdAsync(int id)
        {
            var venta = _ventas.FirstOrDefault(v => v.Id == id);
            return Task.FromResult(venta != null ? MapToResponse(venta) : null);
        }

        public Task<VentaResponseDto> CreateAsync(CrearVentaDto crearDto)
        {
            // Basic validations
            if (crearDto.Detalles == null || !crearDto.Detalles.Any())
                throw new ArgumentException("La venta debe contener al menos un detalle.");

            foreach (var d in crearDto.Detalles)
            {
                if (d.VarianteId <= 0)
                    throw new ArgumentException("Cada detalle debe tener un Id de Variante válido.");
                if (d.Cantidad <= 0)
                    throw new ArgumentException("La cantidad debe ser mayor a 0.");
                if (d.PrecioUnitario <= 0)
                    throw new ArgumentException("El precio unitario debe ser mayor a 0.");
            }

            var detalleEntidades = new List<DetalleVenta>();
            foreach (var d in crearDto.Detalles)
            {
                var detalle = new DetalleVenta
                {
                    Id = _nextDetalleId++,
                    VarianteId = d.VarianteId,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Subtotal = d.Cantidad * d.PrecioUnitario
                };
                detalleEntidades.Add(detalle);
            }

            var total = detalleEntidades.Sum(det => det.Subtotal);
            if (total <= 0)
                throw new ArgumentException("El total de la venta debe ser mayor a 0.");

            var venta = new Venta
            {
                Id = _nextId++,
                Fecha = DateTime.UtcNow,
                Estado = "Pendiente",
                Detalles = detalleEntidades,
                Total = total
            };

            // assign VentaId to each detalle
            foreach (var det in venta.Detalles)
            {
                det.VentaId = venta.Id;
            }

            _ventas.Add(venta);
            var response = MapToResponse(venta);
            return Task.FromResult(response);
        }

        private static VentaResponseDto MapToResponse(Venta venta)
        {
            return new VentaResponseDto
            {
                Id = venta.Id,
                Fecha = venta.Fecha,
                Total = venta.Total,
                Estado = venta.Estado,
                Detalles = venta.Detalles?.Select(d => new DetalleVentaDto
                {
                    Id = d.Id,
                    VarianteId = d.VarianteId,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Subtotal = d.Subtotal
                }).ToList() ?? new List<DetalleVentaDto>()
            };
        }
    }
}
