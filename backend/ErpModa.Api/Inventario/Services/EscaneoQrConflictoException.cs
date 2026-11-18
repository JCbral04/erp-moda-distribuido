namespace ErpModa.Api.Inventario.Services
{
    // El id del escaneo ya se registró para otro SKU: el reintento no corresponde
    // a la misma lectura, así que no se suma ni se devuelve el escaneo anterior.
    public class EscaneoQrConflictoException : Exception
    {
        public EscaneoQrConflictoException(string message) : base(message)
        {
        }
    }
}
