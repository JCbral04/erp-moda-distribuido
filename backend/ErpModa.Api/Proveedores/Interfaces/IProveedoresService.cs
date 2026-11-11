using System.Collections.Generic;
using System.Threading.Tasks;
using ErpModa.Api.Proveedores.DTOs;

namespace ErpModa.Api.Proveedores.Interfaces
{
    public interface IProveedoresService
    {
        Task<IEnumerable<ProveedorResponseDto>> GetAllAsync(bool? activo = null);
        Task<ProveedorResponseDto?> GetByIdAsync(int id);
        Task<ProveedorResponseDto> CreateAsync(CrearProveedorDto crearDto);
        Task<ProveedorResponseDto?> UpdateAsync(int id, ActualizarProveedorDto actualizarDto);
        Task<bool> DeleteAsync(int id);
    }
}

