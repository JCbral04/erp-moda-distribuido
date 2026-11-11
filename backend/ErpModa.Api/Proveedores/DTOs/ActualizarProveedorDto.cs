namespace ErpModa.Api.Proveedores.DTOs
{
    public class ActualizarProveedorDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Nit { get; set; } = string.Empty;
        public string? Contacto { get; set; }
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
        public string? Direccion { get; set; }
        public int TiempoEntregaDias { get; set; }
        public string? FormaPago { get; set; }
        public bool Activo { get; set; } = true;
    }
}

