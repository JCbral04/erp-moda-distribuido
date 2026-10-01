using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Ventas.Models
{
    /// <summary>
    /// Representa una Venta.
    /// </summary>
    [Table("ventas")]
    public class Venta
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("fecha")]
        [Required]
        public DateTime Fecha { get; set; }

        [Column("subtotal")]
        [Required]
        public decimal Subtotal { get; set; }

        [Column("impuesto")]
        [Required]
        public decimal Impuesto { get; set; } = 0;

        [Column("total")]
        [Required]
        public decimal Total { get; set; }

        [Column("metodo_pago")]
        [MaxLength(20)]
        [Required]
        public string MetodoPagoStr { get; set; } = string.Empty;

        [NotMapped]
        public MetodoPago MetodoPago
        {
            get => Enum.Parse<MetodoPago>(MetodoPagoStr);
            set => MetodoPagoStr = value.ToString();
        }

        [Column("estado")]
        [MaxLength(20)]
        [Required]
        public string EstadoStr { get; set; } = string.Empty;

        [NotMapped]
        public EstadoVenta Estado
        {
            get => Enum.Parse<EstadoVenta>(EstadoStr);
            set => EstadoStr = value.ToString();
        }

        [Column("motivo_anulacion")]
        [MaxLength(255)]
        public string? MotivoAnulacion { get; set; }

        [Column("creado_en")]
        [Required]
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        [Column("actualizado_en")]
        [Required]
        public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

        public List<DetalleVenta> Detalles { get; set; } = new();
    }
}