using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpModa.Api.Proveedores.Models
{
    /// <summary>
    /// Registro de bitácora de auditoría para acciones relevantes del módulo Proveedores.
    /// Almacena quién realizó la operación, cuándo, qué entidad, qué operación y datos anteriores/nuevos.
    /// </summary>
    [Table("auditoria_log")]
    public class AuditoriaLog
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("usuario_id")]
        [Required]
        [MaxLength(100)]
        public string UsuarioId { get; set; } = string.Empty;

        [Column("usuario_nombre")]
        [MaxLength(200)]
        public string? UsuarioNombre { get; set; }

        [Column("entidad")]
        [Required]
        [MaxLength(100)]
        public string Entidad { get; set; } = string.Empty;

        [Column("entidad_id")]
        public int? EntidadId { get; set; }

        [Column("operacion")]
        [Required]
        [MaxLength(50)]
        public string Operacion { get; set; } = string.Empty; // Crear, Actualizar, Eliminar, CambiarEstado

        [Column("datos_anteriores", TypeName = "text")]
        public string? DatosAnteriores { get; set; }

        [Column("datos_nuevos", TypeName = "text")]
        public string? DatosNuevos { get; set; }

        [Column("fecha_operacion")]
        [Required]
        public DateTime FechaOperacion { get; set; } = DateTime.UtcNow;

        [Column("direccion_ip")]
        [MaxLength(50)]
        public string? DireccionIp { get; set; }
    }
}
