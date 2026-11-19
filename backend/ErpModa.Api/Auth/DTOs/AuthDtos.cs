using System;
using System.ComponentModel.DataAnnotations;
using ErpModa.Api.Auth.Models;

namespace ErpModa.Api.Auth.DTOs
{
    public class LoginRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class UsuarioResponse
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiraEn { get; set; }
        public UsuarioResponse Usuario { get; set; } = new();
    }

    public class CrearUsuarioRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public RolUsuario Rol { get; set; }
    }

    /// <summary>Solo para la inicialización controlada del primer administrador.</summary>
    public class BootstrapAdminRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string Password { get; set; } = string.Empty;
    }
}
