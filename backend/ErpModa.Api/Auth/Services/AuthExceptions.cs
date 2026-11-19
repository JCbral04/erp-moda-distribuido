using System;

namespace ErpModa.Api.Auth.Services
{
    /// <summary>Mensaje único para correo desconocido y contraseña incorrecta.</summary>
    public class CredencialesInvalidasException : Exception
    {
        public CredencialesInvalidasException()
            : base("Credenciales inválidas.")
        {
        }
    }

    public class UsuarioDuplicadoException : Exception
    {
        public UsuarioDuplicadoException()
            : base("Ya existe un usuario con ese correo.")
        {
        }
    }

    public class InicializacionNoDisponibleException : Exception
    {
        public InicializacionNoDisponibleException(string mensaje) : base(mensaje)
        {
        }
    }

    public class ConfiguracionAuthInvalidaException : Exception
    {
        public ConfiguracionAuthInvalidaException(string mensaje) : base(mensaje)
        {
        }
    }
}
