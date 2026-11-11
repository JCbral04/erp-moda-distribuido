using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpModa.Api.Proveedores.Models
{
    /// <summary>
    /// Detalle de productos en una orden de compra.
    /// </summary>
    [Table("detalles_orden_compra")]
    public class DetalleOrdenCompra
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("orden_compra_id")]
        [Required]
        public int OrdenCompraId { get; set; }

        [Column("producto_id")]
        [Required]
        public int ProductoId { get; set; }

        [Column("cantidad")]
        [Required]
        public int Cantidad { get; set; }

        [Column("precio_unitario")]
        [Required]
        public decimal PrecioUnitario { get; set; }

        [Column("subtotal")]
        [Required]
        public decimal Subtotal { get; set; }

        [ForeignKey("OrdenCompraId")]
        public OrdenCompra? OrdenCompra { get; set; }
    }
}

