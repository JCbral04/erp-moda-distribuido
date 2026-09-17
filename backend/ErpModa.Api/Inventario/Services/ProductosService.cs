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
