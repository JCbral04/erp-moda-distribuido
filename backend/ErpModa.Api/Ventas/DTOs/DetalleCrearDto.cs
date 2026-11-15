using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ErpModa.Api.Ventas.DTOs
{
    /// <summary>
    /// DTO for a single detail in a Venta creation request.
    /// </summary>
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class DetalleCrearDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "El identificador de variante debe ser mayor que cero.")]
        public int VarianteId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor que cero.")]
        public int Cantidad { get; set; }

        [Range(typeof(decimal), "0.01", "9999999999.99",
            ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
            ErrorMessage = "El precio unitario debe ser mayor que cero.")]
        public decimal PrecioUnitario { get; set; }
    }
}