using System;
using System.Collections.Generic;

namespace ErpModa.Api.Proveedores.DTOs
{
    public class OrdenCompraResponseDto
    {
        public int Id { get; set; }
        public int ProveedorId { get; set; }
        public string ProveedorNombre { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Estado { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal Impuesto { get; set; }
        public decimal Total { get; set; }
        public string? Observaciones { get; set; }
        public List<DetalleOrdenCompraDto> Detalles { get; set; } = new();
        public DateTime CreadoEn { get; set; }
    }
}

