namespace ErpModa.Api.Inventario.DTOs
{
    public class ProductoResponseDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal PrecioBase { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public List<VarianteDto> Variantes { get; set; } = new();
    }
}
