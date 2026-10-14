using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Inventario.Models
{
    [Table("variantes")]
    public class Variante
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("producto_id")]
        [Required]
        public int ProductoId { get; set; }

        [Column("talla")]
        [MaxLength(20)]
        [Required]
        public string Talla { get; set; } = string.Empty;

        [Column("color")]
        [MaxLength(50)]
        [Required]
        public string Color { get; set; } = string.Empty;

        [Column("sku")]
        [MaxLength(50)]
        [Required]
        public string Sku { get; set; } = string.Empty;

        [Column("stock")]
        [Required]
        public int Stock { get; set; }

        [Column("sobreprecio")]
        public decimal? Sobreprecio { get; set; }

        [Column("stock_minimo")]
        [Required]
        public int StockMinimo { get; set; } = 5;

        [Column("creado_en")]
        [Required]
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        [Column("actualizado_en")]
        [Required]
        public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

        [ForeignKey("ProductoId")]
        public Producto? Producto { get; set; }

        // Navigation property para movimientos de stock
        public List<MovimientoStock> MovimientosStock { get; set; } = new();
    }
}
