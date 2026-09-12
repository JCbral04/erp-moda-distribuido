using System;
using System.Collections.Generic;
namespace ErpModa.Api.Ventas.Models
{
    /// <summary>
    /// Representa una Venta.
    /// </summary>
    public class Venta
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Total { get; set; }
        public string? Estado { get; set; }
        public List<DetalleVenta>? Detalles { get; set; }
    }
}
