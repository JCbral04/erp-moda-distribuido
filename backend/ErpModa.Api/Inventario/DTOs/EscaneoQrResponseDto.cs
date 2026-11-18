namespace ErpModa.Api.Inventario.DTOs
{
    public class EscaneoQrResponseDto
    {
        public int VarianteId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Producto { get; set; } = string.Empty;
        public string Talla { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int Stock { get; set; }
    }
}
