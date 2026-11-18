using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpModa.Api.Proveedores.OrdenesCompra.Models
{
    [Table("ordenes_compra")]
    public class OrdenCompra
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("proveedor_id")]
        [Required]
        public int ProveedorId { get; set; }

        [Column("fecha")]
        [Required]
        public DateTime Fecha { get; set; } = DateTime.UtcNow;

        [Column("subtotal")]
        [Required]
        public decimal Subtotal { get; set; }

        [Column("total")]
        [Required]
        public decimal Total { get; set; }

        [Column("estado")]
        [Required]
        public EstadoOrdenCompra Estado { get; set; } = EstadoOrdenCompra.Pendiente;

        [Column("observaciones")]
        [MaxLength(500)]
        public string? Observaciones { get; set; }

        [Column("creado_en")]
        [Required]
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        [Column("actualizado_en")]
        [Required]
        public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

        [Column("version")]
        [Required]
        public int Version { get; set; }

        public List<DetalleOrdenCompra> Detalles { get; set; } = new();
    }
}
