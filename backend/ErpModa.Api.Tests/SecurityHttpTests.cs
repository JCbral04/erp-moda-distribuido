using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ErpModa.Api.Tests;

public class SecurityHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SecurityHttpTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task JsonMalFormado_Retorna400_SinDetallesInternos()
    {
        var response = await _client.PostAsync("/api/auth/login",
            new StringContent("{ invalido", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("JsonReaderException", body);
        Assert.DoesNotContain("System.Text.Json", body);
    }

    [Fact]
    public async Task TipoIncompatible_Retorna400_SinNombresDeClases()
    {
        var response = await _client.PostAsync("/api/auth/login",
            new StringContent("{\"email\":123,\"password\":456}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Int32", body);
        Assert.DoesNotContain("String", body);
    }

    [Fact]
    public async Task ExcepcionEnProduccion_Retorna500_SinStackTrace()
    {
        var response = await _client.GetAsync("/__test/exception");
        // En entorno de prueba (Development) el endpoint existe.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return;
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("codigo", out _));
        Assert.True(doc.RootElement.TryGetProperty("mensaje", out _));
        Assert.DoesNotContain("boom interno secreto", body);
        Assert.DoesNotContain("StackTrace", body);
        Assert.DoesNotContain("Exception", body);
    }
}
