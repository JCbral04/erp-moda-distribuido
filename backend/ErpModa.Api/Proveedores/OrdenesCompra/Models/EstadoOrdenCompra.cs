namespace ErpModa.Api.Proveedores.OrdenesCompra.Models
{
    /// <summary>
    /// Estado de una orden de compra.
    /// Pendiente -> Enviada -> Parcial/Recibida, o Cancelada en cualquier momento.
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