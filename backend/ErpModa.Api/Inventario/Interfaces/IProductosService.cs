using ErpModa.Api.Inventario.DTOs;

namespace ErpModa.Api.Inventario.Interfaces
{
    public interface IProductosService
    {
        Task<IEnumerable<ProductoResponseDto>> GetAllAsync();
        Task<ProductoResponseDto?> GetByIdAsync(int id);
        Task<ProductoResponseDto> CreateAsync(CrearProductoDto crearDto);
        Task<ProductoResponseDto?> UpdateAsync(int id, ActualizarProductoDto actualizarDto);
        Task<bool> DeleteAsync(int id);
    }
}
