using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ErpModa.Api.Tests;

public class RateLimitHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RateLimitHttpTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_MasDe10Solicitudes_Retorna429()
    {
        HttpResponseMessage? ultima = null;
        for (var i = 0; i < 11; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Forwarded-For", "192.0.2.1");
            ultima = await _client.SendAsync(request);
        }
        Assert.NotNull(ultima);
        Assert.Equal(HttpStatusCode.TooManyRequests, ultima!.StatusCode);
    }
}
