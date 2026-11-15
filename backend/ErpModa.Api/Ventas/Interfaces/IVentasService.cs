using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpModa.Api.Ventas.DTOs;

namespace ErpModa.Api.Ventas.Interfaces
{
    public interface IVentasService
    {
        Task<IEnumerable<VentaResponseDto>> GetAllAsync(
            string? estado = null,
            DateTime? fecha = null,
            string? usuarioActual = null,
            bool esAdministrador = false);
        Task<VentasResumenDto> GetResumenAsync(
            string? usuarioActual = null,
            bool esAdministrador = false);
        Task<VentaResponseDto?> GetByIdAsync(
            int id,
            string? usuarioActual = null,
            bool esAdministrador = false);
        Task<VentaResponseDto> CreateAsync(CrearVentaDto crearDto, string? usuarioActual = null);
        Task<VentaResponseDto> ConfirmarAsync(
            int id,
            string? usuarioActual = null,
            bool esAdministrador = false);
        Task<VentaResponseDto> AnularAsync(
            int id,
            AnularVentaDto anularDto,
            string? usuarioActual = null,
            bool esAdministrador = false);
    }
}