using System.Collections.Generic;
using System.Threading.Tasks;
using ErpModa.Api.Proveedores.Models;

namespace ErpModa.Api.Proveedores.Interfaces
{
    /// <summary>
    /// Contrato para el servicio de bitácora de auditoría.
    /// </summary>
    public interface IAuditoriaLogService
    {
        Task RegistrarAsync(string usuarioId, string entidad, int? entidadId, string operacion,
            string? datosAnteriores = null, string? datosNuevos = null, string? usuarioNombre = null,
            string? direccionIp = null);
    }
}
