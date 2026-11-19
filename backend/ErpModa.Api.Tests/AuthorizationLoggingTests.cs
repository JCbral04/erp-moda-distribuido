using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ErpModa.Api.Tests;

public class AuthorizationLoggingTests
{
    [Fact]
    public async Task Registro_SinToken_Devuelve401()
    {
        var factory = new AuthTestFactory();
        var client = factory.CreateClient();
        var response = await client.PostAsync("/api/auth/registro",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Registro_ConTokenVendedor_Devuelve403()
    {
        var factory = new AuthTestFactory();
        var client = factory.CreateClient();
        var token = factory.CrearToken(1, "Vendedor");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsync("/api/auth/registro",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
