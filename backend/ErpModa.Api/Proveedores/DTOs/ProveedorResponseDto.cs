namespace ErpModa.Api.Proveedores.DTOs
{
    /// <summary>
    /// DTO for a Proveedor response. El Estado se expone como texto
    /// ("Activo"/"Inactivo") para la API, igual que en Ventas.
    /// </summary>
    public class ProveedorResponseDto
    {
        public int Id { get; set; }
        public string RazonSocial { get; set; } = string.Empty;
        public string Nit { get; set; } = string.Empty;
        public string? Contacto { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
        public string? FormaPago { get; set; }
        public int? TiempoEntrega { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}