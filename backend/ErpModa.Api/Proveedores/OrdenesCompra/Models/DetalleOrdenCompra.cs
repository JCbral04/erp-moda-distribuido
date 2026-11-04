namespace ErpModa.Api.Proveedores.OrdenesCompra.Models
{
    /// <summary>
    /// Línea de una Orden de Compra (producto, cantidad y precio acordado).
    /// ProductoId es una referencia suave a Inventario (sin FK dura),
    /// igual que se hace entre Ventas e Inventario.
    /// </summary>
    public class DetalleOrdenCompra
    {
        public int Id { get; set; }
        public int OrdenCompraId { get; set; }
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }
}