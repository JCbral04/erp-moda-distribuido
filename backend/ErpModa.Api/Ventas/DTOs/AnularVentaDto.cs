using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ErpModa.Api.Ventas.DTOs
{
    /// <summary>
    /// DTO for canceling (anular) an existing Venta. La venta no se elimina, cambia a estado Anulada.
    /// </summary>
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class AnularVentaDto
    {
        [Required(ErrorMessage = "El motivo de anulacion es obligatorio.")]
        [StringLength(255, MinimumLength = 1, ErrorMessage = "El motivo de anulacion no puede superar {1} caracteres.")]
        public string Motivo { get; set; } = string.Empty;
    }
}