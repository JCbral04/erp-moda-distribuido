using System;
using System.Collections.Generic;
namespace ErpModa.Api.Ventas.DTOs
{
    public class VentaResponseDto
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Total { get; set; }
        public string? Estado { get; set; }
        public List<DetalleVentaDto> Detalles { get; set; } = new();
    }
}
