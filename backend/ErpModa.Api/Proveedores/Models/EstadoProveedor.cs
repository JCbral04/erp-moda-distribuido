namespace ErpModa.Api.Proveedores.Models
{
    /// <summary>
    /// Estado operativo de un proveedor. Un proveedor inactivo no puede
    /// recibir nuevas órdenes (RN-003, docs/requerimientos/proveedores.md).
    /// </summary>
    public enum EstadoProveedor
    {
        Activo,
        Inactivo
    }
}