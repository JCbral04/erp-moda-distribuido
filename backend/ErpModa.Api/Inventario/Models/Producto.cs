namespace ErpModa.Api.Inventario.Models
{
    public class Producto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal PrecioBase { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;
        public List<Variante> Variantes { get; set; } = new();
    }
}
