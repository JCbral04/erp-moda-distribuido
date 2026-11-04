using System.Collections.Generic;

namespace ErpModa.Api.Proveedores.OrdenesCompra.DTOs
{
    /// <summary>
    /// DTO de respuesta de una Orden de Compra.
    /// </summary>
    public class OrdenCompraResponseDto
    {
        public int Id { get; set; }
        public int ProveedorId { get; set; }
        public string? ProveedorRazonSocial { get; set; }
        public DateTime Fecha { get; set; }
        public string Estado { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal Total { get; set; }
        public string? Observaciones { get; set; }
        public List<DetalleOrdenCompraDto> Detalles { get; set; } = new();
    }

    /// <summary>
    /// DTO de respuesta de una línea de detalle de Orden de Compra.
    /// </summary>
    public class DetalleOrdenCompraDto
    {
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }
}