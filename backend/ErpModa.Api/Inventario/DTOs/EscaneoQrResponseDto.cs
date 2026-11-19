namespace ErpModa.Api.Inventario.DTOs
{
    public class EscaneoQrResponseDto
    {
        public Guid IdEscaneo { get; set; }
        public int VarianteId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Producto { get; set; } = string.Empty;
        public string Talla { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int Stock { get; set; }

        // true cuando el id ya estaba registrado (reintento del lector): no se
        // sumó otra unidad y Stock es el stock actual de la variante.
        public bool Duplicado { get; set; }
    }
}
