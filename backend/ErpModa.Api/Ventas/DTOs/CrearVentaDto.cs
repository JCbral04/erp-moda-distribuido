using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;

namespace ErpModa.Api.Ventas.DTOs
{
    /// <summary>
    /// DTO for creating a Venta.
    /// </summary>
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class CrearVentaDto : IValidatableObject
    {
        // Fecha removida; se genera en backend
        [Required(ErrorMessage = "La venta debe contener al menos un detalle.")]
        [MinLength(1, ErrorMessage = "La venta debe contener al menos un detalle.")]
        public List<DetalleCrearDto> Detalles { get; set; } = new();

        // Metodo de pago valido: "Efectivo", "Tarjeta" o "Transferencia"
        [Required(ErrorMessage = "El metodo de pago es obligatorio.")]
        [StringLength(20, ErrorMessage = "El metodo de pago no puede superar {1} caracteres.")]
        [RegularExpression("^(?i)(Efectivo|Tarjeta|Transferencia)$",
            ErrorMessage = "El metodo de pago no es valido. Valores validos: Efectivo, Tarjeta, Transferencia.")]
        public string? MetodoPago { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Detalles == null)
            {
                yield break;
            }

            if (Detalles.Any(detalle => detalle == null))
            {
                yield return new ValidationResult(
                    "La lista de detalles no puede contener elementos nulos.",
                    new[] { nameof(Detalles) });
            }
        }
    }
}