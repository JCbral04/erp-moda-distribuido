namespace ErpModa.Api.Ventas.DTOs
{
    /// <summary>
    /// DTO for a single detail in a Venta creation request.
    /// </summary>
    public class DetalleCrearDto
    {
        public int VarianteId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
    }
}
