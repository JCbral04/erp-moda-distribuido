using ErpModa.Api.Proveedores.OrdenesCompra.DTOs;

namespace ErpModa.Api.Proveedores.OrdenesCompra.Interfaces
{
    /// <summary>
    /// Contrato de servicios de órdenes de compra.
    /// </summary>
    public interface IOrdenesCompraService
    {
        Task<IEnumerable<OrdenCompraResponseDto>> GetAllAsync();
        Task<OrdenCompraResponseDto?> GetByIdAsync(int id);
        Task<OrdenCompraResponseDto> CreateAsync(CrearOrdenCompraDto crearDto);
    }
}