using System.Collections.Generic;

namespace ErpModa.Api.Proveedores.OrdenesCompra.DTOs
{
    /// <summary>
    /// DTO para crear una Orden de Compra asociada a un proveedor.
    /// </summary>
    public class CrearOrdenCompraDto
    {
        public int ProveedorId { get; set; }
        public List<DetalleOrdenCompraCrearDto> Detalles { get; set; } = new();
        public string? Observaciones { get; set; }

        /// <summary>
        /// Devuelve la lista de errores de validación (vacía si el DTO es válido).
        /// </summary>
        public List<string> Validar()
        {
            var errores = new List<string>();

            if (ProveedorId <= 0)
                errores.Add("El proveedor es obligatorio.");

            if (Detalles == null || Detalles.Count == 0)
                errores.Add("La orden debe tener al menos un producto.");

            for (var i = 0; i < (Detalles?.Count ?? 0); i++)
            {
                var d = Detalles![i];
                var pref = $"Detalle {i + 1}: ";
                if (d.ProductoId <= 0)
                    errores.Add($"{pref}El producto es obligatorio.");
                if (d.Cantidad <= 0)
                    errores.Add($"{pref}La cantidad debe ser mayor a 0.");
                if (d.PrecioUnitario <= 0)
                    errores.Add($"{pref}El precio unitario debe ser mayor a 0.");
            }

            return errores;
        }
    }
}