using System.Collections.Generic;

namespace ErpModa.Api.Proveedores.DTOs
{
    public class CrearOrdenCompraDto
    {
        public int ProveedorId { get; set; }
        public string? Observaciones { get; set; }
        public List<CrearDetalleOrdenDto> Detalles { get; set; } = new();
    }
}

