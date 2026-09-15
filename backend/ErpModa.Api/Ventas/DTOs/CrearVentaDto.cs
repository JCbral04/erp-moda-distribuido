using System.Collections.Generic;

namespace ErpModa.Api.Ventas.DTOs
{
    /// <summary>
    /// DTO for creating a Venta.
    /// </summary>
    public class CrearVentaDto
    {
        // Fecha removida; se genera en backend
        public List<DetalleCrearDto> Detalles { get; set; } = new();

        // Método de pago válido: "Efectivo", "Tarjeta" o "Transferencia"
        public string? MetodoPago { get; set; }
    }
}