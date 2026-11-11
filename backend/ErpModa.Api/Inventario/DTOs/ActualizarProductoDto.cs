using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ErpModa.Api.Inventario.DTOs
{
    // Los límites reflejan docs/inventario/schema.sql (tabla productos).
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class ActualizarProductoDto
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

        // Obligatorio en el JSON: si faltara, valdría false y el PUT desactivaría
        // el producto sin que el cliente lo pidiera.
        [JsonRequired]
        public bool Activo { get; set; }
    }
}
