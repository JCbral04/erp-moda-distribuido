using System;
namespace ErpModa.Api.Ventas.Models
{
    /// <summary>
    /// Representa el detalle de una venta.
    /// </summary>
    public class DetalleVenta
    {
        public int Id { get; set; }
        public int VentaId { get; set; }
        public int VarianteId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }
}
