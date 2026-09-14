namespace ErpModa.Api.Ventas.DTOs
{
    /// <summary>
    /// Resumen calculado sobre las ventas almacenadas.
    /// </summary>
    public class VentasResumenDto
    {
        public int CantidadVentas { get; set; }
        public int VentasConfirmadas { get; set; }
        public int VentasAnuladas { get; set; }
        public decimal TotalVentasConfirmadas { get; set; }
    }
}