namespace ErpModa.Api.Inventario.Models
{
    public class Variante
    {
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public string Talla { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int Stock { get; set; }
        public decimal? Sobreprecio { get; set; }
    }
}
