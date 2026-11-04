using System.Collections.Generic;

namespace ErpModa.Api.Proveedores.DTOs
{
    /// <summary>
    /// DTO for updating an existing Proveedor. Todos los campos son opcionales:
    /// solo se validan los valores provistos (null = no se modifica). También admite
    /// el cambio de Estado a "Activo"/"Inactivo" (RN-003).
    /// </summary>
    public class ActualizarProveedorDto
    {
        public string? RazonSocial { get; set; }
        public string? Nit { get; set; }
        public string? Contacto { get; set; }
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
        public string? FormaPago { get; set; }
        public int? TiempoEntrega { get; set; }
        public string? Estado { get; set; }

        /// <summary>
        /// Devuelve la lista de errores de validación de los campos provistos (vacía si son válidos).
        /// </summary>
        public List<string> Validar()
        {
            var errores = new List<string>();

            if (RazonSocial != null)
                ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarRazonSocial(RazonSocial), errores);
            if (Nit != null)
                ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarNit(Nit), errores);
            if (Email != null)
                ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarEmail(Email), errores);
            if (Contacto != null)
                ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarContacto(Contacto), errores);
            if (Telefono != null)
                ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarTelefono(Telefono), errores);
            if (Direccion != null)
                ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarDireccion(Direccion), errores);
            if (FormaPago != null)
                ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarFormaPago(FormaPago), errores);
            if (TiempoEntrega.HasValue)
                ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarTiempoEntrega(TiempoEntrega), errores);
            if (Estado != null)
                ProveedorValidaciones.Agregar(ProveedorValidaciones.ValidarEstado(Estado), errores);

            return errores;
        }
    }
}