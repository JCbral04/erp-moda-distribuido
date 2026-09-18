using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Inventario.Models
{
    [Table("movimientos_stock")]
    public class MovimientoStock
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("variante_id")]
        [Required]
        public int VarianteId { get; set; }

        [Column("tipo")]
        [MaxLength(30)]
        [Required]
        public string Tipo { get; set; } = string.Empty;

        [Column("cantidad")]
        [Required]
        public int Cantidad { get; set; }

        [Column("referencia_tipo")]
        [MaxLength(30)]
        public string? ReferenciaTipo { get; set; }

        [Column("referencia_id")]
        public int? ReferenciaId { get; set; }

        [Column("motivo")]
        [MaxLength(255)]
        public string? Motivo { get; set; }

        [Column("usuario")]
        [MaxLength(150)]
        [Required]
        public string Usuario { get; set; } = string.Empty;

        [Column("confianza")]
        public decimal? Confianza { get; set; }

        [Column("creado_en")]
        [Required]
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        [ForeignKey("VarianteId")]
        public Variante? Variante { get; set; }
    }
}
