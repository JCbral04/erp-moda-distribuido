using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ErpModa.Api.Proveedores.Models;

namespace ErpModa.Api.Proveedores.DTOs
{
    /// <summary>
    /// Reglas de validación puramente lógicas del módulo Proveedores, compartidas por los DTOs.
    /// Cada método devuelve null si el valor es válido o un mensaje de error en español.
    /// No usa atributos ni DataAnnotations.
    /// </summary>
    internal static class ProveedorValidaciones
    {
        internal const int NitMaxLength = 20;
        internal const int ContactoMaxLength = 60;
        internal const int DireccionMaxLength = 120;
        internal const int FormaPagoMaxLength = 50;
        internal const int TelefonoMaxLength = 20;
        internal const int EmailMaxLength = 100;
        internal const int TiempoEntregaMaxDias = 365;

        private static readonly Regex EmailPattern =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        // NIT puede ser numérico (Ej.: 901234567-5) o incluir letras (RUT).
        private static readonly Regex NitPattern =
            new(@"^[A-Za-z0-9.\- ]+$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        private static readonly Regex TelefonoPattern =
            new(@"^[0-9+\-(). ]+$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        internal static void Agregar(string? mensaje, List<string> errores)
        {
            if (mensaje != null)
                errores.Add(mensaje);
        }

        internal static string? ValidarRazonSocial(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return "La razón social es obligatoria.";
            return null;
        }

        internal static string? ValidarNit(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return "El NIT/RUT es obligatorio.";

            var v = valor.Trim();
            if (v.Length > NitMaxLength)
                return $"El NIT/RUT no puede superar {NitMaxLength} caracteres.";
            if (!NitPattern.IsMatch(v))
                return "El NIT/RUT solo puede contener letras, números, guiones, puntos o espacios.";

            return null;
        }

        internal static string? ValidarEmail(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return "El correo electrónico es obligatorio.";

            var v = valor.Trim();
            if (v.Length > EmailMaxLength)
                return $"El correo electrónico no puede superar {EmailMaxLength} caracteres.";
            if (!EmailPattern.IsMatch(v))
                return "El correo electrónico no es válido.";

            return null;
        }

        internal static string? ValidarTelefono(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return null;

            var v = valor.Trim();
            if (v.Length > TelefonoMaxLength)
                return $"El teléfono no puede superar {TelefonoMaxLength} caracteres.";
            if (!TelefonoPattern.IsMatch(v))
                return "El teléfono solo puede contener dígitos, espacios y los símbolos + - ( ).";

            return null;
        }

        internal static string? ValidarContacto(string? valor)
        {
            return ValidarLongitudMaxima(valor, ContactoMaxLength, "El contacto");
        }

        internal static string? ValidarDireccion(string? valor)
        {
            return ValidarLongitudMaxima(valor, DireccionMaxLength, "La dirección");
        }

        internal static string? ValidarFormaPago(string? valor)
        {
            return ValidarLongitudMaxima(valor, FormaPagoMaxLength, "La forma de pago");
        }

        internal static string? ValidarTiempoEntrega(int? valor)
        {
            if (!valor.HasValue)
                return null;
            if (valor.Value <= 0)
                return "El tiempo de entrega debe ser mayor a 0 días.";
            if (valor.Value > TiempoEntregaMaxDias)
                return $"El tiempo de entrega no puede superar {TiempoEntregaMaxDias} días.";

            return null;
        }

        internal static string? ValidarEstado(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return null;

            foreach (var estado in Enum.GetValues<EstadoProveedor>())
            {
                if (string.Equals(estado.ToString(), valor.Trim(), StringComparison.OrdinalIgnoreCase))
                    return null;
            }

            return $"El estado '{valor}' no es válido. Valores válidos: {string.Join(", ", Enum.GetNames<EstadoProveedor>())}.";
        }

        private static string? ValidarLongitudMaxima(string? valor, int max, string nombreCampo)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return null;

            var v = valor.Trim();
            if (v.Length > max)
                return $"{nombreCampo} no puede superar {max} caracteres.";

            return null;
        }
    }
}