using System.Collections.Generic;
using System.Threading.Tasks;
using ErpModa.Api.Ventas.DTOs;

namespace ErpModa.Api.Ventas.Interfaces
{
    public interface IVentasService
    {
        Task<IEnumerable<VentaResponseDto>> GetAllAsync();
        Task<VentaResponseDto?> GetByIdAsync(int id);
        Task<VentaResponseDto> CreateAsync(CrearVentaDto crearDto);
    }
}
