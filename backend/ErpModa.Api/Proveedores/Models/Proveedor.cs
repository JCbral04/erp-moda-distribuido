using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpModa.Api.Proveedores.Models
{
    [Table("proveedores")]
    public class Proveedor
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("razon_social")]
        [MaxLength(150)]
        [Required]
        public string RazonSocial { get; set; } = string.Empty;

        [Column("nit")]
        [MaxLength(20)]
        [Required]
        public string Nit { get; set; } = string.Empty;

        [Column("contacto")]
        [MaxLength(60)]
        public string? Contacto { get; set; }

        [Column("email")]
        [MaxLength(100)]
        [Required]
        public string Email { get; set; } = string.Empty;

        [Column("telefono")]
        [MaxLength(20)]
        public string? Telefono { get; set; }

        [Column("direccion")]
        [MaxLength(120)]
        public string? Direccion { get; set; }

        [Column("forma_pago")]
        [MaxLength(50)]
        public string? FormaPago { get; set; }

        [Column("tiempo_entrega")]
        public int? TiempoEntrega { get; set; }

        [Column("estado")]
        [Required]
        public EstadoProveedor Estado { get; set; } = EstadoProveedor.Activo;

        [Column("version")]
        [Required]
        public int Version { get; set; }

        [Column("creado_en")]
        [Required]
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        [Column("actualizado_en")]
        [Required]
        public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    }
}
