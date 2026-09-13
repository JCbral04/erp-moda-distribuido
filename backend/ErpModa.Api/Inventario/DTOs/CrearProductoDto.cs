namespace ErpModa.Api.Inventario.DTOs
{
    public class CrearProductoDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal PrecioBase { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public List<CrearVarianteDto> Variantes { get; set; } = new();
    }
}
