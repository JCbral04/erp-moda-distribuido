using System.Data;
using ErpModa.Api.Data;
using ErpModa.Api.Inventario.DTOs;
using ErpModa.Api.Inventario.Interfaces;
using ErpModa.Api.Inventario.Models;
using Microsoft.EntityFrameworkCore;

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

        // Entrada de una unidad al escanear el QR de la etiqueta (el QR contiene el SKU).
        // Registra un movimiento tipo='deteccion_ia', cantidad=+1; la salida la hace
        // Ventas al confirmar la venta (movimiento tipo='venta', cantidad<0).
        // Devuelve null si no existe una variante con ese SKU.
        public async Task<EscaneoQrResponseDto?> RegistrarEscaneoQrAsync(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku))
                throw new ArgumentException("El SKU es obligatorio.");

            // El lector puede entregar el texto del QR con espacios o saltos de línea.
            var skuNormalizado = sku.Trim();

            EscaneoQrResponseDto? resultado = null;
            await EjecutarAtomicoAsync(async () =>
            {
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
                    Motivo = "Entrada por escaneo de QR",
                    Usuario = "ia-qr",
                    Confianza = 1m,
                    CreadoEn = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                resultado = new EscaneoQrResponseDto
                {
                    VarianteId = variante.Id,
                    Sku = variante.Sku,
                    Producto = variante.Producto?.Nombre ?? string.Empty,
                    Talla = variante.Talla,
                    Color = variante.Color,
                    Stock = variante.Stock
                };
            });

            return resultado;
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
