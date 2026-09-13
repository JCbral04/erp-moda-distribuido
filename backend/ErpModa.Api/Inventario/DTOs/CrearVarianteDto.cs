namespace ErpModa.Api.Inventario.DTOs
{
    public class CrearVarianteDto
    {
        public string Talla { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int Stock { get; set; }
        public decimal? Sobreprecio { get; set; }
    }
}
