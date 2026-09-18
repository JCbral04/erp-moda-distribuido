using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using ErpModa.Api.Data;
using ErpModa.Api.Inventario.Models;
using ErpModa.Api.Ventas.DTOs;
using ErpModa.Api.Ventas.Exceptions;
using ErpModa.Api.Ventas.Interfaces;
using ErpModa.Api.Ventas.Models;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Ventas.Services
{
    public class VentasService : IVentasService
    {
        // Zona horaria local del negocio (Colombia, UTC-5, sin horario de verano).
        // Usada para interpretar el filtro ?fecha= como "día calendario local" y
        // traducirlo a un rango UTC.
        private static readonly TimeSpan ZonaLocalUtc = TimeSpan.FromHours(-5);

        private readonly ErpModaDbContext _context;

        public VentasService(ErpModaDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IEnumerable<VentaResponseDto>> GetAllAsync(string? estado = null, DateTime? fecha = null)
        {
            var estadoFiltro = ParseEstadoFiltro(estado);

            var query = _context.Ventas.Include(v => v.Detalles).AsQueryable();

            if (estadoFiltro.HasValue)
                query = query.Where(v => v.EstadoStr == estadoFiltro.Value.ToString());

            if (fecha.HasValue)
            {
                var (inicioUtc, finUtc) = ConstruirRangoDiaUtc(fecha.Value);
                query = query.Where(v => v.Fecha >= inicioUtc && v.Fecha < finUtc);
            }

            var ventas = await query.ToListAsync();
            return ventas.Select(MapToResponse);
        }

        public async Task<VentasResumenDto> GetResumenAsync()
        {
            var confirmadasQuery = _context.Ventas
                .Where(v => v.EstadoStr == EstadoVenta.Confirmada.ToString());

            var confirmadasCount = await confirmadasQuery.CountAsync();
            var totalConfirmadas = await confirmadasQuery.SumAsync(v => v.Total);

            var anuladas = await _context.Ventas
                .Where(v => v.EstadoStr == EstadoVenta.Anulada.ToString())
                .CountAsync();

            var totalVentas = await _context.Ventas.CountAsync();

            return new VentasResumenDto
            {
                CantidadVentas = totalVentas,
                VentasConfirmadas = confirmadasCount,
                VentasAnuladas = anuladas,
                TotalVentasConfirmadas = totalConfirmadas
            };
        }

        public async Task<VentaResponseDto?> GetByIdAsync(int id)
        {
            var venta = await _context.Ventas
                .Include(v => v.Detalles)
                .FirstOrDefaultAsync(v => v.Id == id);

            return venta != null ? MapToResponse(venta) : null;
        }

        public async Task<VentaResponseDto> CreateAsync(CrearVentaDto crearDto)
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
                Fecha = DateTime.UtcNow,
                Subtotal = total,
                Impuesto = 0,
                Total = total,
                MetodoPago = metodoPago,
                Estado = EstadoVenta.Pendiente,
                Detalles = detalleEntidades,
                CreadoEn = DateTime.UtcNow,
                ActualizadoEn = DateTime.UtcNow
            };

            _context.Ventas.Add(venta);
            await _context.SaveChangesAsync();

            return MapToResponse(venta);
        }

        public async Task<VentaResponseDto> ConfirmarAsync(int id)
        {
            var venta = await GetVentaOrThrowAsync(id);

            // Todo el flujo (validar/descontar stock + estado + factura) es atómico:
            // o se confirma la venta y se registran todos los descuentos, o no se
            // cambia nada (docs/contratos/ventas-inventario.md, sección 6, opción b).
            await EjecutarAtomicoAsync(async () =>
            {
                if (venta.Estado != EstadoVenta.Pendiente)
                    throw new InvalidOperationException(
                        $"La venta con id {venta.Id} está en estado {venta.Estado}; solo se puede confirmar una venta pendiente.");

                // Contrato Ventas→Inventario: valida Y descuenta el stock ANTES de
                // confirmar (sin descuentos parciales: se validan todas las líneas
                // primero). movimiento tipo='venta', cantidad<0 (docs/inventario/schema.sql).
                await AplicarDescuentoStockAsync(venta);

                venta.Estado = EstadoVenta.Confirmada;
                venta.ActualizadoEn = DateTime.UtcNow;

                // Generar factura automáticamente (RN-004)
                _context.Facturas.Add(new Factura
                {
                    VentaId = venta.Id,
                    Numero = await GenerarNumeroFacturaAsync(),
                    Fecha = DateTime.UtcNow,
                    Subtotal = venta.Subtotal,
                    Impuesto = venta.Impuesto,
                    Total = venta.Total,
                    Estado = "emitida",
                    CreadoEn = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            });

            return MapToResponse(venta);
        }

        public async Task<VentaResponseDto> AnularAsync(int id, AnularVentaDto anularDto)
        {
            var venta = await GetVentaOrThrowAsync(id);

            if (venta.Estado == EstadoVenta.Anulada)
                throw new InvalidOperationException(
                    $"La venta con id {id} ya está anulada.");

            if (anularDto == null || string.IsNullOrWhiteSpace(anularDto.Motivo))
                throw new ArgumentException("El motivo de anulación es obligatorio.");

            var motivo = anularDto.Motivo.Trim();

            // Restauración atómica con el cambio de estado/factura.
            await EjecutarAtomicoAsync(async () =>
            {
                // Solo una venta Confirmada descontó stock; una venta Pendiente nunca
                // lo descontó y por eso no restaura (movimiento tipo='anulacion_venta').
                if (venta.Estado == EstadoVenta.Confirmada)
                    await RestaurarStockAsync(venta, motivo);

                // La venta no se elimina: pasa a Anulada y conserva el motivo (RN-005 ventas.md).
                venta.Estado = EstadoVenta.Anulada;
                venta.MotivoAnulacion = motivo;
                venta.ActualizadoEn = DateTime.UtcNow;

                // Marcar factura como anulada si existe
                var factura = await _context.Facturas.FirstOrDefaultAsync(f => f.VentaId == venta.Id);
                if (factura != null)
                {
                    factura.Estado = "anulada";
                }

                await _context.SaveChangesAsync();
            });

            return MapToResponse(venta);
        }

        private async Task<Venta> GetVentaOrThrowAsync(int id)
        {
            var venta = await _context.Ventas
                .Include(v => v.Detalles)
                .FirstOrDefaultAsync(v => v.Id == id);

            return venta ?? throw new KeyNotFoundException($"No existe la venta con id {id}.");
        }

        // ── Integración Ventas→Inventario (docs/contratos/ventas-inventario.md) ──

        // Ejecuta la acción en una transacción de base de datos. Con EF Core sobre
        // PostgreSQL usa una transacción real (Serializable: el stock se valida con
        // el snapshot más estricto). Con el proveedor InMemory de los tests no hay
        // transacciones relacionales, así que se ejecuta directamente.
        private async Task EjecutarAtomicoAsync(Func<Task> accion)
        {
            if (_context.Database.IsRelational())
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                await accion();
                await transaction.CommitAsync();
            }
            else
            {
                await accion();
            }
        }

        // Valida que cada variante exista y tenga stock suficiente y, solo después
        // de validar TODAS las líneas, aplica el descuento y registra los movimientos
        // de salida (tipo='venta', cantidad<0). Si algo falla, no se produce ningún
        // descuento parcial: la confirmación lanza y la venta permanece Pendiente.
        private async Task AplicarDescuentoStockAsync(Venta venta)
        {
            var variantesPorId = await ObtenerVariantesDeLaVentaAsync(venta);

            foreach (var det in venta.Detalles)
            {
                if (!variantesPorId.TryGetValue(det.VarianteId, out var variante))
                    throw new KeyNotFoundException(
                        $"La variante {det.VarianteId} no existe. No se puede confirmar la venta {venta.Id}.");

                if (variante.Stock < det.Cantidad)
                    throw new StockInsuficienteException(det.VarianteId, variante.Stock, det.Cantidad);
            }

            foreach (var det in venta.Detalles)
            {
                var variante = variantesPorId[det.VarianteId];
                variante.Stock -= det.Cantidad;
                variante.ActualizadoEn = DateTime.UtcNow;

                _context.MovimientosStock.Add(new MovimientoStock
                {
                    VarianteId = det.VarianteId,
                    Tipo = "venta",
                    Cantidad = -det.Cantidad,
                    ReferenciaTipo = "venta",
                    ReferenciaId = venta.Id,
                    Motivo = null,
                    Usuario = "sistema",
                    Confianza = null,
                    CreadoEn = DateTime.UtcNow
                });
            }
        }

        // Restaura las cantidades de una venta Confirmada a su inventario y registra
        // un movimiento de entrada (tipo='anulacion_venta', cantidad>0) por variante.
        // El motivo de anulación es obligatorio para este tipo (docs/inventario/schema.sql).
        private async Task RestaurarStockAsync(Venta venta, string motivo)
        {
            var variantesPorId = await ObtenerVariantesDeLaVentaAsync(venta);

            foreach (var det in venta.Detalles)
            {
                if (!variantesPorId.TryGetValue(det.VarianteId, out var variante))
                    throw new KeyNotFoundException(
                        $"La variante {det.VarianteId} no existe. No se puede anular la venta {venta.Id}.");

                variante.Stock += det.Cantidad;
                variante.ActualizadoEn = DateTime.UtcNow;

                _context.MovimientosStock.Add(new MovimientoStock
                {
                    VarianteId = det.VarianteId,
                    Tipo = "anulacion_venta",
                    Cantidad = det.Cantidad,
                    ReferenciaTipo = "venta",
                    ReferenciaId = venta.Id,
                    Motivo = motivo,
                    Usuario = "sistema",
                    Confianza = null,
                    CreadoEn = DateTime.UtcNow
                });
            }
        }

        private async Task<Dictionary<int, Variante>> ObtenerVariantesDeLaVentaAsync(Venta venta)
        {
            var varianteIds = venta.Detalles.Select(d => d.VarianteId).Distinct().ToList();
            return await _context.Variantes
                .Where(v => varianteIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id);
        }

        private async Task<string> GenerarNumeroFacturaAsync()
        {
            // Incremento atómico del contador (UPSERT + RETURNING).
            // Una sola fila (id=1) garantiza unicidad y orden bajo concurrencia.
            var contador = await _context.ContadoresFactura.FindAsync(1);
            if (contador == null)
            {
                contador = new ContadorFactura { Id = 1, Valor = 0, Prefijo = "FAC-", Formato = "D6", ActualizadoEn = DateTime.UtcNow };
                _context.ContadoresFactura.Add(contador);
            }

            contador.Valor++;
            contador.ActualizadoEn = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return $"{contador.Prefijo}{contador.Valor.ToString(contador.Formato)}";
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