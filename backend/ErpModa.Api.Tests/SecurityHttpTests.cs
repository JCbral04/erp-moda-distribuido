using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ErpModa.Api.Tests;

[ApiController]
[Route("__test")]
public class ThrowingController : ControllerBase
{
    [HttpGet("exception")]
    public IActionResult Get() => throw new Exception("boom interno secreto");
}

public class SecurityHttpTests
{
    private readonly HttpClient _client;
    private readonly HttpClient _productionClient;

    public SecurityHttpTests()
    {
        var devFactory = new WebApplicationFactory<Program>();
        devFactory = devFactory.WithWebHostBuilder(b => b.UseEnvironment("Development"));
        _client = devFactory.CreateClient();

        var prodFactory = new WebApplicationFactory<Program>();
        prodFactory = prodFactory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("Jwt:Key", Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
            b.UseSetting("Jwt:Issuer", "ErpModa");
            b.UseSetting("Jwt:Audience", "ErpModa.Client");
            b.ConfigureServices(s => s.AddControllers().AddApplicationPart(typeof(ThrowingController).Assembly));
        });
        _productionClient = prodFactory.CreateClient();
    }

    [Fact]
    public async Task JsonMalFormado_Retorna400_SinDetallesInternos()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent("{ invalido", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Forwarded-For", "192.0.2.10");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("JsonReaderException", body);
        Assert.DoesNotContain("System.Text.Json", body);
    }

    [Fact]
    public async Task TipoIncompatible_Retorna400_SinNombresDeClases()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent("{\"email\":123,\"password\":456}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Forwarded-For", "192.0.2.11");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Int32", body);
        Assert.DoesNotContain("String", body);
    }

    [Fact]
    public async Task ExcepcionEnProduccion_Retorna500_SinStackTrace()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/__test/exception");
        request.Headers.Add("X-Forwarded-For", "192.0.2.12");
        var response = await _productionClient.SendAsync(request);
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
