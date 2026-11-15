using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ErpModa.Api.Ventas.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ErpModa.Api.Tests;

public class VentasDtoValidationHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public VentasDtoValidationHttpTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private static StringContent Json(string json) =>
        new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task PostVentas_SinDetalles_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas",
            Json("{\"metodoPago\":\"Efectivo\",\"detalles\":[]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostVentas_MetodoPagoInvalido_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas",
            Json("{\"metodoPago\":\"Bitcoin\",\"detalles\":[{\"varianteId\":1,\"cantidad\":1,\"precioUnitario\":10}]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostVentas_PropiedadDesconocida_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas",
            Json("{\"metodoPago\":\"Efectivo\",\"detalles\":[{\"varianteId\":1,\"cantidad\":1,\"precioUnitario\":10}],\"foo\":1}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostVentas_TipoIncorrecto_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas",
            Json("{\"metodoPago\":\"Efectivo\",\"detalles\":[{\"varianteId\":\"abc\",\"cantidad\":1,\"precioUnitario\":10}]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostVentas_DetalleConCantidadCero_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas",
            Json("{\"metodoPago\":\"Efectivo\",\"detalles\":[{\"varianteId\":1,\"cantidad\":0,\"precioUnitario\":10}]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostVentas_DetalleConPrecioCero_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas",
            Json("{\"metodoPago\":\"Efectivo\",\"detalles\":[{\"varianteId\":1,\"cantidad\":1,\"precioUnitario\":0}]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostVentas_SinMetodoPago_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas",
            Json("{\"detalles\":[{\"varianteId\":1,\"cantidad\":1,\"precioUnitario\":10}]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostVentas_PropiedadDesconocidaEnDetalle_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas",
            Json("{\"metodoPago\":\"Efectivo\",\"detalles\":[{\"varianteId\":1,\"cantidad\":1,\"precioUnitario\":10,\"bar\":2}]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostAnular_Venta_PropiedadDesconocida_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas/1/anular",
            Json("{\"motivo\":\"error\",\"extra\":true}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostAnular_Venta_MotivoVacio_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas/1/anular",
            Json("{\"motivo\":\"\"}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostAnular_Venta_MotivoDemasiadoLargo_Retorna400()
    {
        var motivo = new string('a', 256);
        var response = await _client.PostAsync("/api/ventas/1/anular",
            Json($"{{\"motivo\":\"{motivo}\"}}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    private static async Task AssertNoStackTrace(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at ErpModa", body, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task PostVentas_DetallesNull_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas",
            Json("{\"metodoPago\":\"Efectivo\",\"detalles\":null}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostVentas_DetalleNull_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas",
            Json("{\"metodoPago\":\"Efectivo\",\"detalles\":[null]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostVentas_VarianteIdCero_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas",
            Json("{\"metodoPago\":\"Efectivo\",\"detalles\":[{\"varianteId\":0,\"cantidad\":1,\"precioUnitario\":10}]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostVentas_VarianteIdNegativo_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas",
            Json("{\"metodoPago\":\"Efectivo\",\"detalles\":[{\"varianteId\":-1,\"cantidad\":1,\"precioUnitario\":10}]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostVentas_CantidadNegativa_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas",
            Json("{\"metodoPago\":\"Efectivo\",\"detalles\":[{\"varianteId\":1,\"cantidad\":-1,\"precioUnitario\":10}]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostVentas_PrecioNegativo_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas",
            Json("{\"metodoPago\":\"Efectivo\",\"detalles\":[{\"varianteId\":1,\"cantidad\":1,\"precioUnitario\":-10}]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostVentas_MetodoPagoTipoIncorrecto_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas",
            Json("{\"metodoPago\":123,\"detalles\":[{\"varianteId\":1,\"cantidad\":1,\"precioUnitario\":10}]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostVentas_MetodoPagoDemasiadoLargo_Retorna400()
    {
        var metodoPago = new string('A', 21);
        var response = await _client.PostAsync("/api/ventas",
            Json($"{{\"metodoPago\":\"{metodoPago}\",\"detalles\":[{{\"varianteId\":1,\"cantidad\":1,\"precioUnitario\":10}}]}}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }

    [Fact]
    public async Task PostAnular_Venta_MotivoNull_Retorna400()
    {
        var response = await _client.PostAsync("/api/ventas/1/anular",
            Json("{\"motivo\":null}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoStackTrace(response);
    }}
