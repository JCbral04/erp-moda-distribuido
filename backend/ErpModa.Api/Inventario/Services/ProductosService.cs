using ErpModa.Api.Inventario.DTOs;
using ErpModa.Api.Inventario.Interfaces;
using ErpModa.Api.Inventario.Models;

namespace ErpModa.Api.Inventario.Services
{
    public class ProductosService : IProductosService
    {
        private readonly List<Producto> _productos = new();
        private int _nextId = 1;
        private int _nextVarianteId = 1;

        public Task<IEnumerable<ProductoResponseDto>> GetAllAsync()
        {
            var result = _productos.Select(MapToResponse);
            return Task.FromResult(result);
        }

        public Task<ProductoResponseDto?> GetByIdAsync(int id)
        {
            var producto = _productos.FirstOrDefault(p => p.Id == id);
            return Task.FromResult(producto != null ? MapToResponse(producto) : null);
        }

        public Task<ProductoResponseDto> CreateAsync(CrearProductoDto crearDto)
        {
            if (string.IsNullOrWhiteSpace(crearDto.Nombre))
                throw new ArgumentException("El nombre del producto es obligatorio.");
            if (crearDto.PrecioBase <= 0)
                throw new ArgumentException("El precio base debe ser mayor a 0.");

            var variantes = (crearDto.Variantes ?? new List<CrearVarianteDto>())
                .Select(v => new Variante
                {
                    Id = _nextVarianteId++,
                    Talla = v.Talla,
                    Color = v.Color,
                    Sku = v.Sku,
                    Stock = v.Stock,
                    Sobreprecio = v.Sobreprecio
                })
                .ToList();

            var producto = new Producto
            {
                Id = _nextId++,
                Nombre = crearDto.Nombre,
                Descripcion = crearDto.Descripcion,
                PrecioBase = crearDto.PrecioBase,
                Categoria = crearDto.Categoria,
                Activo = true,
                Variantes = variantes
            };

            foreach (var variante in producto.Variantes)
            {
                variante.ProductoId = producto.Id;
            }

            _productos.Add(producto);
            return Task.FromResult(MapToResponse(producto));
        }

        public Task<ProductoResponseDto?> UpdateAsync(int id, ActualizarProductoDto actualizarDto)
        {
            var producto = _productos.FirstOrDefault(p => p.Id == id);
            if (producto == null)
                return Task.FromResult<ProductoResponseDto?>(null);

            if (string.IsNullOrWhiteSpace(actualizarDto.Nombre))
                throw new ArgumentException("El nombre del producto es obligatorio.");
            if (actualizarDto.PrecioBase <= 0)
                throw new ArgumentException("El precio base debe ser mayor a 0.");

            producto.Nombre = actualizarDto.Nombre;
            producto.Descripcion = actualizarDto.Descripcion;
            producto.PrecioBase = actualizarDto.PrecioBase;
            producto.Categoria = actualizarDto.Categoria;
            producto.Activo = actualizarDto.Activo;

            return Task.FromResult<ProductoResponseDto?>(MapToResponse(producto));
        }

        public Task<bool> DeleteAsync(int id)
        {
            var producto = _productos.FirstOrDefault(p => p.Id == id);
            if (producto == null)
                return Task.FromResult(false);

            _productos.Remove(producto);
            return Task.FromResult(true);
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
