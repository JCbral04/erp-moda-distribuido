using System;

namespace ErpModa.Api.Ventas.Exceptions
{
    /// <summary>
    /// Indica que una variante no tiene stock suficiente para confirmar una venta.
    /// El controlador lo mapea a 409 Conflict, consistente con el contrato
    /// Ventas->Inventario (docs/contratos/ventas-inventario.md, respuesta
    /// "Stock insuficiente - 409 Conflict").
    /// </summary>
    public class StockInsuficienteException : Exception
    {
        public int VarianteId { get; }
        public int Disponible { get; }
        public int Solicitado { get; }

        public StockInsuficienteException(int varianteId, int disponible, int solicitado)
            : base($"Stock insuficiente para la variante {varianteId}: disponible {disponible}, solicitado {solicitado}.")
        {
            VarianteId = varianteId;
            Disponible = disponible;
            Solicitado = solicitado;
        }
    }
}