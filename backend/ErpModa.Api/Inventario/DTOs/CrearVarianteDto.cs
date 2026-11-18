using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ErpModa.Api.Inventario.DTOs
{
    // Los límites reflejan docs/inventario/schema.sql (tabla variantes).
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class CrearVarianteDto
    {
        [Required(ErrorMessage = "La talla es obligatoria.")]
        [StringLength(20, ErrorMessage = "La talla no puede superar {1} caracteres.")]
        public string Talla { get; set; } = string.Empty;

        [Required(ErrorMessage = "El color es obligatorio.")]
        [StringLength(50, ErrorMessage = "El color no puede superar {1} caracteres.")]
        public string Color { get; set; } = string.Empty;

        [Required(ErrorMessage = "El SKU es obligatorio.")]
        [StringLength(50, ErrorMessage = "El SKU no puede superar {1} caracteres.")]
        public string Sku { get; set; } = string.Empty;

        [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
        public int Stock { get; set; }

        [Range(typeof(decimal), "0", "9999999999.99",
            ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
            ErrorMessage = "El sobreprecio debe estar entre {1} y {2}.")]
        public decimal? Sobreprecio { get; set; }
    }
}
