using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpModa.Api.Auth.Models
{
    /// <summary>Usuario local del ERP. La contraseña nunca se guarda en texto plano.</summary>
    [Table("usuarios")]
    public class Usuario
    {
        private string _email = string.Empty;

        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        /// Correo normalizado (minúsculas, sin espacios periféricos) para que
        /// "Admin@erp.com" y "admin@erp.com" no puedan crear dos cuentas.
        /// </summary>
        [Column("email")]
        [MaxLength(256)]
        [Required]
        public string Email
        {
            get => _email;
            set => _email = NormalizarEmail(value);
        }

        [Column("password_hash")]
        [MaxLength(255)]
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("rol")]
        [MaxLength(20)]
        [Required]
        public RolUsuario Rol { get; set; }

        [Column("creado_en")]
        [Required]
        public DateTime CreadoEn { get; set; }

        [Column("actualizado_en")]
        [Required]
        public DateTime ActualizadoEn { get; set; }

        public static string NormalizarEmail(string? email) =>
            email?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
