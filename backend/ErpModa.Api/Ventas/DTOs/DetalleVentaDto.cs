using System;
namespace ErpModa.Api.Ventas.DTOs
{
    /// <summary>
    /// DTO for DetalleVenta.
    /// </summary>
    public class DetalleVentaDto
    {
        public int Id { get; set; }
        public int VarianteId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }
}
