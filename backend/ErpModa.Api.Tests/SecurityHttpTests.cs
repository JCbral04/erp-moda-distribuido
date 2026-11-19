using System.Net;
using System.Net.Http;
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
    public async Task Login_MasDe10Solicitudes_Retorna429()
    {
        HttpResponseMessage? ultima = null;
        for (var i = 0; i < 11; i++)
        {
            ultima = await _client.PostAsync("/api/auth/login",
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        }
        Assert.NotNull(ultima);
        Assert.Equal(HttpStatusCode.TooManyRequests, ultima!.StatusCode);
    }
}
