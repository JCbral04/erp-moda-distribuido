namespace ErpModa.Api.Ventas.DTOs
{
    /// <summary>
    /// DTO for canceling (ano) an existing Venta. La venta no se elimina, cambia a estado Anulada.
    /// </summary>
    public class AnularVentaDto
    {
        public string Motivo { get; set; } = string.Empty;
    }
}