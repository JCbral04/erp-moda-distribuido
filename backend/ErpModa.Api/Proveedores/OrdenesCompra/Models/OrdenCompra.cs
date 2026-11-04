using System;
using System.Collections.Generic;

namespace ErpModa.Api.Proveedores.OrdenesCompra.Models
{
    /// <summary>
    /// Representa una Orden de Compra asociada a un Proveedor.
    /// Clase plana de dominio: sin anotaciones de ORM (mapeo en ErpModaDbContext).
    /// </summary>
    public class OrdenCompra
    {
        public int Id { get; set; }
        public int ProveedorId { get; set; }
        public DateTime Fecha { get; set; } = DateTime.UtcNow;
        public decimal Subtotal { get; set; }
        public decimal Total { get; set; }
        public EstadoOrdenCompra Estado { get; set; } = EstadoOrdenCompra.Pendiente;
        public string? Observaciones { get; set; }
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
        public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

        public List<DetalleOrdenCompra> Detalles { get; set; } = new();
    }
}