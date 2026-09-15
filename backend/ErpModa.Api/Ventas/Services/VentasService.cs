using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErpModa.Api.Ventas.DTOs;
using ErpModa.Api.Ventas.Interfaces;
using ErpModa.Api.Ventas.Models;

namespace ErpModa.Api.Ventas.Services
{
    public class VentasService : IVentasService
    {
        // Zona horaria local del negocio (Colombia, UTC-5, sin horario de verano).
        // Usada para interpretar el filtro ?fecha= como "día calendario local" y
        // traducirlo a un rango UTC.
        private static readonly TimeSpan ZonaLocalUtc = TimeSpan.FromHours(-5);

        // Todas las lecturas/escrituras sobre _ventas, _nextId y _nextDetalleId
        // se serializan con este candado para evitar condiciones de carrera.
        private readonly object _syncLock = new object();
        private readonly List<Venta> _ventas = new();

        // Interlocked.Increment exige que la lectura inicial no sea 1 (incrementa
        // antes de asignar), por eso arrancan en 0: la primera venta/detalle es 1.
        private int _nextId = 0;
        private int _nextDetalleId = 0;

        public Task<IEnumerable<VentaResponseDto>> GetAllAsync(string? estado = null, DateTime? fecha = null)
        {
            var estadoFiltro = ParseEstadoFiltro(estado);

            lock (_syncLock)
            {
                IEnumerable<Venta> query = _ventas;

                if (estadoFiltro.HasValue)
                    query = query.Where(v => v.Estado == estadoFiltro.Value);

                if (fecha.HasValue)
                {
                    var (inicioUtc, finUtc) = ConstruirRangoDiaUtc(fecha.Value);
                    query = query.Where(v => v.Fecha >= inicioUtc && v.Fecha < finUtc);
                }

                // Materializar dentro del lock: evita el 500 "Collection was modified"
                // si otro hilo muta _ventas mientras se serializa el resultado.
                return Task.FromResult<IEnumerable<VentaResponseDto>>(query.Select(MapToResponse).ToList());
            }
        }

        public Task<VentasResumenDto> GetResumenAsync()
        {
            lock (_syncLock)
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
        }

        public Task<VentaResponseDto?> GetByIdAsync(int id)
        {
            lock (_syncLock)
            {
                var venta = _ventas.FirstOrDefault(v => v.Id == id);
                return Task.FromResult(venta != null ? MapToResponse(venta) : null);
            }
        }

        public Task<VentaResponseDto> CreateAsync(CrearVentaDto crearDto)
        {
            var metodoPago = ParseMetodoPago(crearDto.MetodoPago);

            if (crearDto.Detalles == null || !crearDto.Detalles.Any())
                throw new ArgumentException("La venta debe contener al menos un detalle.");

            lock (_syncLock)
            {
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
                        Id = Interlocked.Increment(ref _nextDetalleId),
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
                    Id = Interlocked.Increment(ref _nextId),
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
        }

        public Task<VentaResponseDto> ConfirmarAsync(int id)
        {
            lock (_syncLock)
            {
                var venta = GetVentaOrThrow(id);

                if (venta.Estado != EstadoVenta.Pendiente)
                    throw new InvalidOperationException(
                        $"La venta con id {id} está en estado {venta.Estado}; solo se puede confirmar una venta pendiente.");

                // Contrato Ventas→Inventario (docs/contratos/ventas-inventario.md):
                // aquí se debe validar y descontar el stock ANTES de confirmar.
                // Descomentar cuando el contrato esté aprobado e implementado:
                // DescontarStockInventario(venta);

                venta.Estado = EstadoVenta.Confirmada;
                return Task.FromResult(MapToResponse(venta));
            }
        }

        public Task<VentaResponseDto> AnularAsync(int id, AnularVentaDto anularDto)
        {
            lock (_syncLock)
            {
                var venta = GetVentaOrThrow(id);

                if (venta.Estado == EstadoVenta.Anulada)
                    throw new InvalidOperationException(
                        $"La venta con id {id} ya está anulada.");

                if (anularDto == null || string.IsNullOrWhiteSpace(anularDto.Motivo))
                    throw new ArgumentException("El motivo de anulación es obligatorio.");

                // La venta no se elimina: pasa a Anulada y conserva el motivo (RN-005 ventas.md).
                // Contrato Ventas→Inventario: aquí se debe restaurar el stock (anulacion_venta).
                // Descomentar cuando el contrato esté aprobado e implementado:
                // DescontarStockInventario(venta);

                venta.Estado = EstadoVenta.Anulada;
                venta.MotivoAnulacion = anularDto.Motivo.Trim();
                return Task.FromResult(MapToResponse(venta));
            }
        }

        private Venta GetVentaOrThrow(int id)
        {
            return _ventas.FirstOrDefault(v => v.Id == id)
                ?? throw new KeyNotFoundException($"No existe la venta con id {id}.");
        }

        // Stub temporal de la integración Ventas→Inventario. Lanza NotImplementedException
        // a propósito para que CI falle de forma explícita si alguien la activa antes de
        // tiempo. Se invocará desde ConfirmarAsync/AnularAsync (descomentar las llamadas)
        // cuando se apruebe e implemente el contrato en docs/contratos/ventas-inventario.md.
        private static void DescontarStockInventario(Venta venta)
        {
            throw new NotImplementedException("Pendiente: Integración con módulo de inventario");
        }

        private static (DateTime inicioUtc, DateTime finUtc) ConstruirRangoDiaUtc(DateTime fecha)
        {
            // "fecha" se interpreta como el día calendario en la zona local del negocio
            // (UTC-5). Se reconstruye el día ignorando hora y Kind para que el rango
            // represente siempre el día completo local, expresado en UTC:
            //   inicioUtc = 2026-09-14T05:00:00Z   (medianoche local)
            //   finUtc    = 2026-09-15T05:00:00Z
            var diaLocal = new DateTime(fecha.Year, fecha.Month, fecha.Day);
            var inicioUtc = new DateTimeOffset(diaLocal, ZonaLocalUtc).UtcDateTime;
            return (inicioUtc, inicioUtc.AddDays(1));
        }

        private static TEnum ParseEnumByName<TEnum>(string? value, string nombreCampo)
            where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"El {nombreCampo} es obligatorio.");

            foreach (var v in Enum.GetValues<TEnum>())
            {
                if (string.Equals(v.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                    return v;
            }

            throw new ArgumentException(
                $"El {nombreCampo} '{value}' no es válido. Valores válidos: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        }

        private static MetodoPago ParseMetodoPago(string? metodoPago)
            => ParseEnumByName<MetodoPago>(metodoPago, "método de pago");

        private static EstadoVenta? ParseEstadoFiltro(string? estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
                return null;
            return ParseEnumByName<EstadoVenta>(estado, "filtro estado");
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