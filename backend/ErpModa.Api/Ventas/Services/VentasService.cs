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

        public Task<IEnumerable<VentaResponseDto>> GetAllAsync(string? estado = null, DateTime? fecha = null)
        {
            var estadoFiltro = ParseEstadoFiltro(estado);

            IEnumerable<Venta> query = _ventas;

            if (estadoFiltro.HasValue)
                query = query.Where(v => v.Estado == estadoFiltro.Value);

            if (fecha.HasValue)
                query = query.Where(v => v.Fecha.Date == fecha.Value.Date);

            return Task.FromResult(query.Select(MapToResponse));
        }

        public Task<VentasResumenDto> GetResumenAsync()
        {
            var confirmadas = _ventas.Where(v => v.Estado == EstadoVenta.Confirmada).ToList();

            return Task.FromResult(new VentasResumenDto
            {
                CantidadVentas = _ventas.Count,
                VentasConfirmadas = confirmadas.Count,
                VentasAnuladas = _ventas.Count(v => v.Estado == EstadoVenta.Anulada),
                TotalVentasConfirmadas = confirmadas.Sum(v => v.Total)
            });
        }

        public Task<VentaResponseDto?> GetByIdAsync(int id)
        {
            var venta = _ventas.FirstOrDefault(v => v.Id == id);
            return Task.FromResult(venta != null ? MapToResponse(venta) : null);
        }

        public Task<VentaResponseDto> CreateAsync(CrearVentaDto crearDto)
        {
            var metodoPago = ParseMetodoPago(crearDto.MetodoPago);

            if (crearDto.Detalles == null || !crearDto.Detalles.Any())
                throw new ArgumentException("La venta debe contener al menos un detalle.");

            var detalleEntidades = new List<DetalleVenta>();
            foreach (var d in crearDto.Detalles)
            {
                if (d.VarianteId <= 0)
                    throw new ArgumentException("Cada detalle debe tener un Id de Variante válido.");
                if (d.Cantidad <= 0)
                    throw new ArgumentException("La cantidad debe ser mayor a 0.");
                if (d.PrecioUnitario <= 0)
                    throw new ArgumentException("El precio unitario debe ser mayor a 0.");

                detalleEntidades.Add(new DetalleVenta
                {
                    Id = _nextDetalleId++,
                    VarianteId = d.VarianteId,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Subtotal = d.Cantidad * d.PrecioUnitario
                });
            }

            var total = detalleEntidades.Sum(det => det.Subtotal);
            if (total <= 0)
                throw new ArgumentException("El total de la venta debe ser mayor a 0.");

            var venta = new Venta
            {
                Id = _nextId++,
                Fecha = DateTime.UtcNow,
                Estado = EstadoVenta.Pendiente,
                MetodoPago = metodoPago,
                Detalles = detalleEntidades,
                Total = total
            };

            foreach (var det in venta.Detalles)
            {
                det.VentaId = venta.Id;
            }

            _ventas.Add(venta);
            return Task.FromResult(MapToResponse(venta));
        }

        public Task<VentaResponseDto> ConfirmarAsync(int id)
        {
            var venta = _ventas.FirstOrDefault(v => v.Id == id)
                ?? throw new KeyNotFoundException($"No existe la venta con id {id}.");

            if (venta.Estado != EstadoVenta.Pendiente)
                throw new InvalidOperationException(
                    $"La venta con id {id} está en estado {venta.Estado}; solo se puede confirmar una venta pendiente.");

            // Contrato Ventas→Inventario (docs/contratos/ventas-inventario.md):
            // aquí se invocará la validación y el descuento de stock (VarianteId, Cantidad)
            // antes de marcar la venta como Confirmada. No se implementa todavía: el
            // contrato debe ser aprobado por el equipo antes de escribir la integración.

            venta.Estado = EstadoVenta.Confirmada;
            return Task.FromResult(MapToResponse(venta));
        }

        public Task<VentaResponseDto> AnularAsync(int id, AnularVentaDto anularDto)
        {
            var venta = _ventas.FirstOrDefault(v => v.Id == id)
                ?? throw new KeyNotFoundException($"No existe la venta con id {id}.");

            if (venta.Estado == EstadoVenta.Anulada)
                throw new InvalidOperationException(
                    $"La venta con id {id} ya está anulada.");

            if (anularDto == null || string.IsNullOrWhiteSpace(anularDto.Motivo))
                throw new ArgumentException("El motivo de anulación es obligatorio.");

            // La venta no se elimina: pasa a Anulada y conserva el motivo (RN-005 ventas.md).
            // El contrato Ventas→Inventario prevé la restauración de stock (anulacion_venta);
            // no se implementa todavía.
            venta.Estado = EstadoVenta.Anulada;
            venta.MotivoAnulacion = anularDto.Motivo.Trim();
            return Task.FromResult(MapToResponse(venta));
        }

        private static MetodoPago ParseMetodoPago(string? metodoPago)
        {
            if (string.IsNullOrWhiteSpace(metodoPago))
                throw new ArgumentException("El método de pago es obligatorio.");

            foreach (var valor in Enum.GetValues<MetodoPago>())
            {
                if (string.Equals(valor.ToString(), metodoPago.Trim(), StringComparison.OrdinalIgnoreCase))
                    return valor;
            }

            throw new ArgumentException(
                $"El método de pago '{metodoPago}' no es válido. Valores válidos: {string.Join(", ", Enum.GetNames<MetodoPago>())}.");
        }

        private static EstadoVenta? ParseEstadoFiltro(string? estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
                return null;

            foreach (var valor in Enum.GetValues<EstadoVenta>())
            {
                if (string.Equals(valor.ToString(), estado.Trim(), StringComparison.OrdinalIgnoreCase))
                    return valor;
            }

            throw new ArgumentException(
                $"El filtro estado '{estado}' no es válido. Valores válidos: {string.Join(", ", Enum.GetNames<EstadoVenta>())}.");
        }

        private static VentaResponseDto MapToResponse(Venta venta)
        {
            return new VentaResponseDto
            {
                Id = venta.Id,
                Fecha = venta.Fecha,
                Total = venta.Total,
                Estado = venta.Estado.ToString(),
                MetodoPago = venta.MetodoPago.ToString(),
                MotivoAnulacion = venta.MotivoAnulacion,
                Detalles = venta.Detalles.Select(d => new DetalleVentaDto
                {
                    Id = d.Id,
                    VarianteId = d.VarianteId,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Subtotal = d.Subtotal
                }).ToList()
            };
        }
    }
}