using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ErpModa.Api.Data;
using ErpModa.Api.Proveedores.Interfaces;
using ErpModa.Api.Proveedores.Models;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Proveedores.Services
{
    /// <summary>
    /// Implementación del servicio de bitácora de auditoría.
    /// Registra en base de datos cada acción relevante para trazabilidad.
    /// </summary>
    public class AuditoriaLogService : IAuditoriaLogService
    {
        private readonly ErpModaDbContext _context;

        public AuditoriaLogService(ErpModaDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task RegistrarAsync(string usuarioId, string entidad, int? entidadId, string operacion,
            string? datosAnteriores = null, string? datosNuevos = null, string? usuarioNombre = null,
            string? direccionIp = null)
        {
            var log = new AuditoriaLog
            {
                UsuarioId = usuarioId ?? string.Empty,
                UsuarioNombre = usuarioNombre,
                Entidad = entidad,
                EntidadId = entidadId,
                Operacion = operacion,
                DatosAnteriores = datosAnteriores,
                DatosNuevos = datosNuevos,
                FechaOperacion = DateTime.UtcNow,
                DireccionIp = direccionIp
            };

            _context.AuditoriaLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
