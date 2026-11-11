using System.Collections.Generic;
using System.Threading.Tasks;
using ErpModa.Api.Proveedores.DTOs;

namespace ErpModa.Api.Proveedores.Interfaces
{
    public interface IOrdenesCompraService
    {
        Task<IEnumerable<OrdenCompraResponseDto>> GetAllAsync(int? proveedorId = null, string? estado = null);
        Task<OrdenCompraResponseDto?> GetByIdAsync(int id);
        Task<OrdenCompraResponseDto> CreateAsync(CrearOrdenCompraDto crearDto);
    }
}

