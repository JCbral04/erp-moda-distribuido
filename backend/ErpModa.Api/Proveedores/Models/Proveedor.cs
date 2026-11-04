namespace ErpModa.Api.Proveedores.Models
{
    /// <summary>
    /// Representa un Proveedor.
    /// Clase plana de dominio: sin decoradores, anotaciones ni dependencias de ORM.
    /// La validación vive en los DTOs (ErpModa.Api.Proveedores.DTOs).
    /// </summary>
    public class Proveedor
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
        public EstadoProveedor Estado { get; set; } = EstadoProveedor.Activo;
    }
}