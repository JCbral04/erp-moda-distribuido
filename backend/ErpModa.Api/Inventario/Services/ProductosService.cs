using System.Data;
using ErpModa.Api.Data;
using ErpModa.Api.Inventario.DTOs;
using ErpModa.Api.Inventario.Interfaces;
using ErpModa.Api.Inventario.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ErpModa.Api.Inventario.Services
{
    public class ProductosService : IProductosService
    {
        private readonly ErpModaDbContext _context;

        public ProductosService(ErpModaDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IEnumerable<ProductoResponseDto>> GetAllAsync()
        {
            var productos = await _context.Productos
                .Include(p => p.Variantes)
                .ToListAsync();

            return productos.Select(MapToResponse);
        }

        public async Task<ProductoResponseDto?> GetByIdAsync(int id)
        {
            var producto = await _context.Productos
                .Include(p => p.Variantes)
                .FirstOrDefaultAsync(p => p.Id == id);

            return producto != null ? MapToResponse(producto) : null;
        }

        public async Task<ProductoResponseDto> CreateAsync(CrearProductoDto crearDto)
        {
            if (string.IsNullOrWhiteSpace(crearDto.Nombre))
                throw new ArgumentException("El nombre del producto es obligatorio.");
            if (crearDto.PrecioBase <= 0)
                throw new ArgumentException("El precio base debe ser mayor a 0.");

            var producto = new Producto
            {
                Nombre = crearDto.Nombre,
                Descripcion = crearDto.Descripcion,
                PrecioBase = crearDto.PrecioBase,
                Categoria = crearDto.Categoria,
                Activo = true,
                CreadoEn = DateTime.UtcNow,
                ActualizadoEn = DateTime.UtcNow,
                Variantes = (crearDto.Variantes ?? new List<CrearVarianteDto>())
                    .Select(v => new Variante
                    {
                        Talla = v.Talla,
                        Color = v.Color,
                        Sku = v.Sku,
                        Stock = v.Stock,
                        Sobreprecio = v.Sobreprecio,
                        StockMinimo = 5,
                        CreadoEn = DateTime.UtcNow,
                        ActualizadoEn = DateTime.UtcNow
                    })
                    .ToList()
            };

            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();

            return MapToResponse(producto);
        }

        public async Task<ProductoResponseDto?> UpdateAsync(int id, ActualizarProductoDto actualizarDto)
        {
            var producto = await _context.Productos
                .Include(p => p.Variantes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (producto == null)
                return null;

            if (string.IsNullOrWhiteSpace(actualizarDto.Nombre))
                throw new ArgumentException("El nombre del producto es obligatorio.");
            if (actualizarDto.PrecioBase <= 0)
                throw new ArgumentException("El precio base debe ser mayor a 0.");

            producto.Nombre = actualizarDto.Nombre;
            producto.Descripcion = actualizarDto.Descripcion;
            producto.PrecioBase = actualizarDto.PrecioBase;
            producto.Categoria = actualizarDto.Categoria;
            producto.Activo = actualizarDto.Activo;
            producto.ActualizadoEn = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MapToResponse(producto);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return false;

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();

            return true;
        }

        // Prefijo del motivo de los movimientos del lector de QR. El motivo lleva el id
        // del escaneo y sirve de clave de idempotencia (índice único uq_mov_escaneo_qr).
        public const string PrefijoMotivoEscaneoQr = "Escaneo QR ";

        public static string MotivoEscaneoQr(Guid idEscaneo) => PrefijoMotivoEscaneoQr + idEscaneo.ToString("D");

        // Entrada de una unidad al escanear el QR de la etiqueta (el QR contiene el SKU).
        // Registra un movimiento tipo='deteccion_ia', cantidad=+1; la salida la hace
        // Ventas al confirmar la venta (movimiento tipo='venta', cantidad<0).
        // Es idempotente por idEscaneo: si el lector reintenta un escaneo que ya se
        // registró (p. ej. se perdió la respuesta), devuelve el registro con
        // Duplicado = true y no suma otra unidad.
        // Devuelve null si no existe una variante con ese SKU.
        // PostgreSQL aborta una transacción Serializable cuando dos de ellas se
        // solapan (40001) o se bloquean entre sí (40P01). Se reintenta SOLO esa
        // transacción, con pocos intentos y espera corta; cualquier otro error
        // (SKU inexistente, id duplicado con otro SKU, fallo de BD) se propaga.
        private const int MaxIntentosEscaneoQr = 3;

        public static bool EsConflictoSerializacion(PostgresException ex) =>
            EsConflictoSerializacion(ex.SqlState);

        public static bool EsConflictoSerializacion(string sqlState) =>
            sqlState == "40001" || sqlState == "40P01";

        public async Task<EscaneoQrResponseDto?> RegistrarEscaneoQrAsync(string sku, Guid idEscaneo)
        {
            if (string.IsNullOrWhiteSpace(sku))
                throw new ArgumentException("El SKU es obligatorio.");
            if (idEscaneo == Guid.Empty)
                throw new ArgumentException("El id del escaneo es obligatorio.");

            // El lector puede entregar el texto del QR con espacios o saltos de línea.
            var skuNormalizado = sku.Trim();
            var motivo = MotivoEscaneoQr(idEscaneo);

            for (var intento = 1; intento <= MaxIntentosEscaneoQr; intento++)
            {
                EscaneoQrResponseDto? resultado = null;
                try
                {
                    await EjecutarAtomicoAsync(async () =>
                    {
                        // Se busca dentro de la misma transacción Serializable que el incremento:
                        // dos peticiones simultáneas con el mismo id no pueden registrar ambas.
                        var previo = await _context.MovimientosStock
                            .Include(m => m.Variante!)
                            .ThenInclude(v => v.Producto)
                            .FirstOrDefaultAsync(m => m.Tipo == "deteccion_ia" && m.Motivo == motivo);

                        if (previo?.Variante != null)
                        {
                            if (previo.Variante.Sku != skuNormalizado)
                                throw new EscaneoQrConflictoException(
                                    $"El escaneo {idEscaneo} ya se registró para el SKU '{previo.Variante.Sku}'.");

                            resultado = CrearRespuestaEscaneo(previo.Variante, idEscaneo, duplicado: true);
                            return;
                        }

                        var variante = await _context.Variantes
                            .Include(v => v.Producto)
                            .FirstOrDefaultAsync(v => v.Sku == skuNormalizado);

                        if (variante == null)
                            return;

                        variante.Stock += 1;
                        variante.ActualizadoEn = DateTime.UtcNow;

                        // El QR se decodifica de forma exacta: no hay incertidumbre que
                        // revisar manualmente (RN-003 ia.md), por eso confianza = 1.
                        _context.MovimientosStock.Add(new MovimientoStock
                        {
                            VarianteId = variante.Id,
                            Tipo = "deteccion_ia",
                            Cantidad = 1,
                            ReferenciaTipo = null,
                            ReferenciaId = null,
                            Motivo = motivo,
                            Usuario = "ia-qr",
                            Confianza = 1m,
                            CreadoEn = DateTime.UtcNow
                        });

                        await _context.SaveChangesAsync();

                        resultado = CrearRespuestaEscaneo(variante, idEscaneo, duplicado: false);
                    });

                    return resultado;
                }
                catch (PostgresException ex) when (intento < MaxIntentosEscaneoQr && EsConflictoSerializacion(ex))
                {
                    // La transacción se abortó: se descartan sus cambios y se repite
                    // completa con el MISMO idEscaneo. Si la primera llegó a commit,
                    // el reintento encuentra el motivo y responde duplicado.
                    _context.ChangeTracker.Clear();
                    await Task.Delay(TimeSpan.FromMilliseconds(50d * (1 << (intento - 1))));
                }
            }

            return null; // inalcanzable: el último intento propaga su excepción
        }

        private static EscaneoQrResponseDto CrearRespuestaEscaneo(Variante variante, Guid idEscaneo, bool duplicado)
        {
            return new EscaneoQrResponseDto
            {
                IdEscaneo = idEscaneo,
                VarianteId = variante.Id,
                Sku = variante.Sku,
                Producto = variante.Producto?.Nombre ?? string.Empty,
                Talla = variante.Talla,
                Color = variante.Color,
                Stock = variante.Stock,
                Duplicado = duplicado
            };
        }

        // Mismo patrón que VentasService: en PostgreSQL la lectura y el incremento del
        // stock van en una transacción Serializable para que dos escaneos simultáneos
        // del mismo SKU no pierdan una unidad. El proveedor InMemory de los tests no
        // tiene transacciones relacionales, así que se ejecuta directamente.
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

        private static ProductoResponseDto MapToResponse(Producto producto)
        {
            return new ProductoResponseDto
            {
                Id = producto.Id,
                Nombre = producto.Nombre,
                Descripcion = producto.Descripcion,
                PrecioBase = producto.PrecioBase,
                Categoria = producto.Categoria,
                Activo = producto.Activo,
                Variantes = producto.Variantes.Select(v => new VarianteDto
                {
                    Id = v.Id,
                    Talla = v.Talla,
                    Color = v.Color,
                    Sku = v.Sku,
                    Stock = v.Stock,
                    Sobreprecio = v.Sobreprecio
                }).ToList()
            };
        }
    }
}
