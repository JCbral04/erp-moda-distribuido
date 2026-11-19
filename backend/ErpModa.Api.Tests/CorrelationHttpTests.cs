using System.Net;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ErpModa.Api.Tests;

public class CorrelationHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CorrelationHttpTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Solicitud_SinCorrelationId_RecibeUnoGenerado()
    {
        var response = await _client.GetAsync("/health");
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
        Assert.False(string.IsNullOrWhiteSpace(response.Headers.GetValues("X-Correlation-ID").First()));
    }

    [Fact]
    public async Task Solicitud_ConCorrelationIdValido_LoConserva()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-ID", "corr-123");
        var response = await _client.SendAsync(request);
        Assert.Equal("corr-123", response.Headers.GetValues("X-Correlation-ID").First());
    }

    [Fact]
    public async Task Solicitud_ConCorrelationIdInvalido_GeneraNuevo()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-ID", "invalido con espacios");
        var response = await _client.SendAsync(request);
        Assert.NotEqual("invalido con espacios", response.Headers.GetValues("X-Correlation-ID").First());
    }
}
