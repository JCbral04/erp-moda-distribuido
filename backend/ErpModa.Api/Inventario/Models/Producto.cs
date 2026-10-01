using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Inventario.Models
{
    [Table("productos")]
    public class Producto
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("nombre")]
        [MaxLength(150)]
        [Required]
        public string Nombre { get; set; } = string.Empty;

        [Column("descripcion")]
        public string? Descripcion { get; set; }

        [Column("precio_base")]
        [Required]
        public decimal PrecioBase { get; set; }

        [Column("categoria")]
        [MaxLength(100)]
        [Required]
        public string Categoria { get; set; } = string.Empty;

        [Column("activo")]
        [Required]
        public bool Activo { get; set; } = true;

        [Column("creado_en")]
        [Required]
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        [Column("actualizado_en")]
        [Required]
        public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

        public List<Variante> Variantes { get; set; } = new();
    }
}
