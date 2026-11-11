using System.Collections.Generic;
using System.Threading.Tasks;
using ErpModa.Api.Proveedores.DTOs;
using ErpModa.Api.Proveedores.Models;

namespace ErpModa.Api.Proveedores.Interfaces
{
    /// <summary>
    /// Contrato del servicio de Workflow para cambios de estado del módulo Proveedores.
    /// Toda transición de estado debe pasar por este servicio para garantizar trazabilidad
    /// y reglas de negocio consistentes (RN-003, etc.).
    /// </summary>
    public interface IWorkflowService
    {
        Task<ProveedorResponseDto> CambiarEstadoProveedorAsync(int proveedorId, string nuevoEstado,
            string usuarioId, string? usuarioNombre = null, string? direccionIp = null);

        Task<OrdenCompraResponseDto> CambiarEstadoOrdenAsync(int ordenId, string nuevoEstado,
            string usuarioId, string? usuarioNombre = null, string? direccionIp = null);
    }
}
