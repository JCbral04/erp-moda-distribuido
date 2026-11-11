using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpModa.Api.Proveedores.Models
{
    /// <summary>
    /// Entidad para órdenes de compra a proveedores.
    /// </summary>
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

        [Column("estado")]
        [MaxLength(20)]
        [Required]
        public string EstadoStr { get; set; } = EstadoOrdenCompra.Pendiente.ToString();

        [NotMapped]
        public EstadoOrdenCompra Estado
        {
            get => Enum.Parse<EstadoOrdenCompra>(EstadoStr);
            set => EstadoStr = value.ToString();
        }

        [Column("subtotal")]
        [Required]
        public decimal Subtotal { get; set; }

        [Column("impuesto")]
        [Required]
        public decimal Impuesto { get; set; } = 0;

        [Column("total")]
        [Required]
        public decimal Total { get; set; }

        [Column("observaciones")]
        [MaxLength(500)]
        public string? Observaciones { get; set; }

        [Column("creado_en")]
        [Required]
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        [Column("actualizado_en")]
        [Required]
        public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

        [ForeignKey("ProveedorId")]
        public Proveedor? Proveedor { get; set; }

        public List<DetalleOrdenCompra> Detalles { get; set; } = new();
    }
}

