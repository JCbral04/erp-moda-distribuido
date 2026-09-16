using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpModa.Api.Ventas.DTOs;

namespace ErpModa.Api.Ventas.Interfaces
{
    public interface IVentasService
    {
        Task<IEnumerable<VentaResponseDto>> GetAllAsync(string? estado = null, DateTime? fecha = null);
        Task<VentasResumenDto> GetResumenAsync();
        Task<VentaResponseDto?> GetByIdAsync(int id);
        Task<VentaResponseDto> CreateAsync(CrearVentaDto crearDto);
        Task<VentaResponseDto> ConfirmarAsync(int id);
        Task<VentaResponseDto> AnularAsync(int id, AnularVentaDto anularDto);
    }
}