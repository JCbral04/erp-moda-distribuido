using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpModa.Api.Ventas.Models
{
    /// <summary>
    /// Contador atómico para generar números de factura secuenciales.
    /// Una sola fila (id=1) evita race conditions: UPDATE ... SET valor = valor + 1 RETURNING valor.
    /// </summary>
    [Table("contador_factura")]
    public class ContadorFactura
    {
        [Key]
        [Column("id")]
        public int Id { get; set; } = 1;

        [Column("valor")]
        [Required]
        public long Valor { get; set; } = 0;

        [Column("prefijo")]
        [MaxLength(10)]
        [Required]
        public string Prefijo { get; set; } = "FAC-";

        [Column("formato")]
        [MaxLength(20)]
        [Required]
        public string Formato { get; set; } = "D6";

        [Column("actualizado_en")]
        [Required]
        public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    }
}