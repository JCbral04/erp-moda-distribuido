using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ErpModa.Api.Inventario.DTOs
{
    // Cuerpo que envía el lector de QR (ai/deteccion.py): el QR de la etiqueta
    // solo contiene el SKU de la variante.
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class EscaneoQrDto
    {
        [Required(ErrorMessage = "El SKU es obligatorio.")]
        [StringLength(50, ErrorMessage = "El SKU no puede superar {1} caracteres.")]
        public string Sku { get; set; } = string.Empty;
    }
}
