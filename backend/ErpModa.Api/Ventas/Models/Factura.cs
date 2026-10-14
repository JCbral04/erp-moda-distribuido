using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Ventas.Models
{
    /// <summary>
    /// Representa una Factura.
    /// </summary>
    [Table("facturas")]
    public class Factura
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("venta_id")]
        [Required]
        public int VentaId { get; set; }

        [Column("numero")]
        [MaxLength(30)]
        [Required]
        public string Numero { get; set; } = string.Empty;

        [Column("fecha")]
        [Required]
        public DateTime Fecha { get; set; } = DateTime.UtcNow;

        [Column("cliente_id")]
        public int? ClienteId { get; set; }

        [Column("cliente_nombre")]
        [MaxLength(150)]
        public string? ClienteNombre { get; set; }

        [Column("cliente_documento")]
        [MaxLength(30)]
        public string? ClienteDocumento { get; set; }

        [Column("subtotal")]
        [Required]
        public decimal Subtotal { get; set; }

        [Column("impuesto")]
        [Required]
        public decimal Impuesto { get; set; } = 0;

        [Column("total")]
        [Required]
        public decimal Total { get; set; }

        [Column("estado")]
        [MaxLength(20)]
        [Required]
        public string Estado { get; set; } = "emitida";

        [Column("creado_en")]
        [Required]
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        [ForeignKey("VentaId")]
        public Venta? Venta { get; set; }

        // Navigation property para incluir detalles de la venta en la factura
        // No se mapea a columna adicional; usa la relación a través de Venta
        [NotMapped]
        public List<DetalleVenta>? Detalles { get; set; }
    }
}
