using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Ventas.Models
{
    /// <summary>
    /// Representa el detalle de una venta.
    /// </summary>
    [Table("detalle_venta")]
    public class DetalleVenta
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("venta_id")]
        [Required]
        public int VentaId { get; set; }

        [Column("variante_id")]
        [Required]
        public int VarianteId { get; set; }

        [Column("cantidad")]
        [Required]
        public int Cantidad { get; set; }

        [Column("precio_unitario")]
        [Required]
        public decimal PrecioUnitario { get; set; }

        [Column("subtotal")]
        [Required]
        public decimal Subtotal { get; set; }

        [ForeignKey("VentaId")]
        public Venta? Venta { get; set; }
    }
}
