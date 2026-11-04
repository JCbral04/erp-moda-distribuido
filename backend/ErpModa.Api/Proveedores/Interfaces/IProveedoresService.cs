using ErpModa.Api.Proveedores.DTOs;

namespace ErpModa.Api.Proveedores.Interfaces
{
    /// <summary>
    /// Contrato de servicios del catálogo de proveedores.
    /// </summary>
    public interface IProveedoresService
    {
        Task<IEnumerable<ProveedorResponseDto>> GetAllAsync();
        Task<ProveedorResponseDto?> GetByIdAsync(int id);
        Task<ProveedorResponseDto> CreateAsync(CrearProveedorDto crearDto);
        Task<ProveedorResponseDto?> UpdateAsync(int id, ActualizarProveedorDto actualizarDto);
        Task<bool> DeleteAsync(int id);
    }
}