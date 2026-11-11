using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpModa.Api.Proveedores.Models
{
    /// <summary>
    /// Entidad Proveedor para la gestión del catálogo de proveedores.
    /// </summary>
    [Table("proveedores")]
    public class Proveedor
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("nombre")]
        [MaxLength(150)]
        [Required]
        public string Nombre { get; set; } = string.Empty;

        [Column("nit")]
        [MaxLength(50)]
        [Required]
        public string Nit { get; set; } = string.Empty;

        [Column("contacto")]
        [MaxLength(150)]
        public string? Contacto { get; set; }

        [Column("telefono")]
        [MaxLength(50)]
        public string? Telefono { get; set; }

        [Column("correo")]
        [MaxLength(100)]
        public string? Correo { get; set; }

        [Column("direccion")]
        [MaxLength(250)]
        public string? Direccion { get; set; }

        [Column("tiempo_entrega_dias")]
        [Required]
        public int TiempoEntregaDias { get; set; } = 0;

        [Column("forma_pago")]
        [MaxLength(100)]
        public string? FormaPago { get; set; }

        [Column("activo")]
        [Required]
        public bool Activo { get; set; } = true;

        [Column("creado_en")]
        [Required]
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        [Column("actualizado_en")]
        [Required]
        public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

        public List<OrdenCompra> OrdenesCompra { get; set; } = new();
    }
}

