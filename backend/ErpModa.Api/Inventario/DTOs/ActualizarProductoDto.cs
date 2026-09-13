namespace ErpModa.Api.Inventario.DTOs
{
    public class ActualizarProductoDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal PrecioBase { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public bool Activo { get; set; }
    }
}
