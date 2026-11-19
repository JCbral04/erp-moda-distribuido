using System.Text.RegularExpressions;

namespace ErpModa.Api.Middleware;

public class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-ID";
    private const int MaxLength = 64;
    private static readonly Regex ValidPattern = new("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = IsValid(incoming) ? incoming! : Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await _next(context);

            if (context.Response.StatusCode is 401 or 403)
            {
                _logger.LogWarning("Solicitud rechazada StatusCode={StatusCode} CorrelationId={CorrelationId} Path={Path}",
                    context.Response.StatusCode, correlationId, context.Request.Path);
            }
        }
    }

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Length > MaxLength) return false;
        // Rechaza caracteres de control e inyeccion en logs.
        if (value.Any(c => char.IsControl(c))) return false;
        return ValidPattern.IsMatch(value);
    }

    public static string Get(HttpContext context) =>
        context.Items.TryGetValue("CorrelationId", out var v) && v is string s ? s : Guid.NewGuid().ToString("N");
}
