using System.Collections.Generic;

namespace ErpModa.Api.Proveedores.DTOs
{
    /// <summary>
    /// DTO for creating a Proveedor (HU-001, docs/requerimientos/proveedores.md).
    /// Validación puramente lógica en Validar(); sin atributos ni DataAnnotations.
    /// </summary>
    public class CrearProveedorDto
    {
        public string? RazonSocial { get; set; }
        public string? Nit { get; set; }
        public string? Contacto { get; set; }
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
        public string? FormaPago { get; set; }
        public int? TiempoEntrega { get; set; }

        /// <summary>
        /// Devuelve la lista de errores de validación (vacía si el DTO es válido).
        /// </summary>
        public List<string> Validar()
        {
            var errores = new List<string>();

            ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarRazonSocial(RazonSocial), errores);
            ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarNit(Nit), errores);
            ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarEmail(Email), errores);
            ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarContacto(Contacto), errores);
            ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarTelefono(Telefono), errores);
            ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarDireccion(Direccion), errores);
            ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarFormaPago(FormaPago), errores);
            ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarTiempoEntrega(TiempoEntrega), errores);

            return errores;
        }
    }
}