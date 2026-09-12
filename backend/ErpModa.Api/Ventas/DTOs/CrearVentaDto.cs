using System;
using System.Collections.Generic;
namespace ErpModa.Api.Ventas.DTOs
{
    /// <summary>
    /// DTO for creating a Venta.
    /// </summary>
    public class CrearVentaDto
    {
        // Fecha removida; se genera en backend
        public List<DetalleCrearDto> Detalles { get; set; } = new();
    }
    public class DetalleCrearDto
    {
        public int VarianteId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
    }
}
