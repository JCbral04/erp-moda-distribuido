using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ErpModa.Api.Inventario.DTOs
{
    // Cuerpo que envía el lector de QR (ai/deteccion.py). El QR de la etiqueta
    // solo contiene el SKU; el lector genera un id por cada escaneo y lo repite
    // en los reintentos para que el backend no sume dos veces la misma unidad.
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class EscaneoQrDto : IValidatableObject
    {
        [Required(ErrorMessage = "El SKU es obligatorio.")]
        [StringLength(50, ErrorMessage = "El SKU no puede superar {1} caracteres.")]
        public string Sku { get; set; } = string.Empty;

        [JsonRequired]
        public Guid IdEscaneo { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IdEscaneo == Guid.Empty)
                yield return new ValidationResult(
                    "El id del escaneo es obligatorio.", new[] { nameof(IdEscaneo) });
        }
    }
}
