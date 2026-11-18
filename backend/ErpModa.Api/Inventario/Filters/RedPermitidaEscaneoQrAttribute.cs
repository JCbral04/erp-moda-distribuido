using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErpModa.Api.Inventario.Filters
{
    // Restricción TEMPORAL del escaneo de QR mientras no exista autenticación
    // (JWT en #82, roles de Inventario en #84). No es un sistema de identidad:
    // solo limita desde qué redes se acepta la petición.
    //
    // - Development: se acepta (docker-compose.override.yml publica el backend
    //   solo en 127.0.0.1).
    // - Cualquier otro entorno: se rechaza con 403 salvo que la IP de origen esté
    //   en EscaneoQr:RedesPermitidas (variable EscaneoQr__RedesPermitidas), una
    //   lista separada por comas de IPs o redes CIDR. Sin configurar, nadie pasa.
    //
    // La IP de origen es la que deja UseForwardedHeaders (#93): detrás del proxy de
    // confianza es la del cliente real, no la del proxy.
    //
    // Cuando llegue #84 este atributo se reemplaza por [Authorize] con la política
    // que se defina para el lector.
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class RedPermitidaEscaneoQrAttribute : Attribute, IAuthorizationFilter
    {
        public const string ClaveConfiguracion = "EscaneoQr:RedesPermitidas";

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var servicios = context.HttpContext.RequestServices;
            if (servicios.GetRequiredService<IHostEnvironment>().IsDevelopment())
                return;

            var redes = servicios.GetRequiredService<IConfiguration>()[ClaveConfiguracion];
            if (EsIpPermitida(context.HttpContext.Connection.RemoteIpAddress, redes))
                return;

            // Sin la IP en el log: se registra el rechazo, no datos del cliente (#88).
            servicios.GetRequiredService<ILoggerFactory>()
                .CreateLogger<RedPermitidaEscaneoQrAttribute>()
                .LogWarning("Escaneo QR rechazado: el origen no está en {Clave}.", ClaveConfiguracion);

            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Origen no permitido para el escaneo de QR.",
                Detail = $"El escaneo de QR solo se acepta desde las redes configuradas en {ClaveConfiguracion}."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        // Entradas inválidas se ignoran: nunca conceden acceso.
        public static bool EsIpPermitida(IPAddress? ip, string? redesPermitidas)
        {
            if (ip == null || string.IsNullOrWhiteSpace(redesPermitidas))
                return false;

            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

            var entradas = redesPermitidas.Split(
                new[] { ',', ';' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var entrada in entradas)
            {
                if (entrada.Contains('/'))
                {
                    if (IPNetwork.TryParse(entrada, out var red) && red.Contains(ip))
                        return true;
                }
                else if (IPAddress.TryParse(entrada, out var direccion))
                {
                    if (direccion.IsIPv4MappedToIPv6)
                        direccion = direccion.MapToIPv4();
                    if (direccion.Equals(ip))
                        return true;
                }
            }

            return false;
        }
    }
}
