using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ErpModa.Api.Inventario.DTOs
{
    // Los límites reflejan docs/inventario/schema.sql (tabla productos).
    // [ApiController] valida también cada CrearVarianteDto de la lista.
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class CrearProductoDto : IValidatableObject
    {
        [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
        [StringLength(150, ErrorMessage = "El nombre no puede superar {1} caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        [Range(typeof(decimal), "0.01", "9999999999.99",
            ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
            ErrorMessage = "El precio base debe estar entre {1} y {2}.")]
        public decimal PrecioBase { get; set; }

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        [StringLength(100, ErrorMessage = "La categoría no puede superar {1} caracteres.")]
        public string Categoria { get; set; } = string.Empty;

        public List<CrearVarianteDto> Variantes { get; set; } = new();

        // Rechaza en la petición lo que la base de datos rechazaría con un 500:
        // SKU repetido (uq_variantes_sku) y talla+color repetidos
        // (uq_variantes_producto_talla_color). Ambas restricciones distinguen
        // mayúsculas, así que la comparación también.
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Variantes == null)
                yield break;

            if (Variantes.Any(v => v == null))
            {
                yield return new ValidationResult(
                    "La lista de variantes no puede contener elementos nulos.", new[] { nameof(Variantes) });
                yield break;
            }

            var skuRepetido = Variantes
                .GroupBy(v => v.Sku ?? string.Empty, StringComparer.Ordinal)
                .FirstOrDefault(g => g.Count() > 1);
            if (skuRepetido != null)
                yield return new ValidationResult(
                    $"El SKU '{skuRepetido.Key}' está repetido en las variantes.", new[] { nameof(Variantes) });

            var combinacionRepetida = Variantes
                .GroupBy(v => (v.Talla, v.Color))
                .FirstOrDefault(g => g.Count() > 1);
            if (combinacionRepetida != null)
                yield return new ValidationResult(
                    $"La combinación talla '{combinacionRepetida.Key.Talla}' y color '{combinacionRepetida.Key.Color}' está repetida en las variantes.",
                    new[] { nameof(Variantes) });
        }
    }
}
