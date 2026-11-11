namespace ErpModa.Api.Proveedores.Models
{
    /// <summary>
    /// Ciclo de vida de una orden de compra.
    /// </summary>
    public enum EstadoOrdenCompra
    {
        Pendiente,
        Enviada,
        Parcial,
        Recibida,
        Cancelada
    }
}

