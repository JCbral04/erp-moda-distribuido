namespace ErpModa.Api.Proveedores.OrdenesCompra.DTOs
{
    /// <summary>
    /// DTO para una línea de detalle al crear una Orden de Compra.
    /// </summary>
    public class DetalleOrdenCompraCrearDto
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
    }
}