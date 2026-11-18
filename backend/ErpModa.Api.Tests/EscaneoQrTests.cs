using System.Net;
using System.Text;
using ErpModa.Api.Data;
using ErpModa.Api.Inventario.DTOs;
using ErpModa.Api.Inventario.Filters;
using ErpModa.Api.Inventario.Models;
using ErpModa.Api.Inventario.Services;
using ErpModa.Api.Ventas.DTOs;
using ErpModa.Api.Ventas.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ErpModa.Api.Tests;

// Entrada de stock por escaneo de QR: el QR de la etiqueta contiene el SKU,
// escanearlo suma una unidad y confirmar una venta la resta.
public class EscaneoQrServiceTests : IDisposable
{
    private const string Sku = "CAM-NEG-M";

    private readonly ErpModaDbContext _context;
    private readonly ProductosService _service;

    public EscaneoQrServiceTests()
    {
        var options = new DbContextOptionsBuilder<ErpModaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ErpModaDbContext(options);
        _service = new ProductosService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<Variante> CrearVarianteAsync(int stock, string sku = Sku, string talla = "M")
    {
        var producto = new Producto
        {
            Nombre = "Camiseta básica",
            Categoria = "Camisetas",
            PrecioBase = 45000m,
            Activo = true
        };
        var variante = new Variante
        {
            Producto = producto,
            Talla = talla,
            Color = "Negro",
            Sku = sku,
            Stock = stock
        };
        _context.Productos.Add(producto);
        _context.Variantes.Add(variante);
        await _context.SaveChangesAsync();
        return variante;
    }

    private Task<EscaneoQrResponseDto?> EscanearAsync(string sku = Sku) =>
        _service.RegistrarEscaneoQrAsync(sku, Guid.NewGuid());

    [Fact]
    public async Task Escanear_SkuExistente_SumaUnaUnidad()
    {
        var variante = await CrearVarianteAsync(stock: 3);

        var resultado = await EscanearAsync();

        Assert.NotNull(resultado);
        Assert.Equal(variante.Id, resultado.VarianteId);
        Assert.Equal(4, resultado.Stock);
        Assert.False(resultado.Duplicado);
        Assert.Equal("Camiseta básica", resultado.Producto);
        Assert.Equal(4, (await _context.Variantes.FindAsync(variante.Id))!.Stock);
    }

    [Fact]
    public async Task Escanear_RegistraMovimientoDeteccionIaConElIdDelEscaneo()
    {
        var variante = await CrearVarianteAsync(stock: 0);
        var idEscaneo = Guid.NewGuid();

        await _service.RegistrarEscaneoQrAsync(Sku, idEscaneo);

        var movimiento = Assert.Single(_context.MovimientosStock);
        Assert.Equal(variante.Id, movimiento.VarianteId);
        Assert.Equal("deteccion_ia", movimiento.Tipo);
        Assert.Equal(1, movimiento.Cantidad);
        Assert.Null(movimiento.ReferenciaTipo);
        Assert.Equal(1m, movimiento.Confianza);
        Assert.Equal($"Escaneo QR {idEscaneo:D}", movimiento.Motivo);
    }

    // Dos prendas iguales son dos lecturas distintas, cada una con su id.
    [Fact]
    public async Task Escanear_VariasPrendasDelMismoSku_SumaCadaEscaneo()
    {
        await CrearVarianteAsync(stock: 0);

        for (var i = 0; i < 3; i++)
            await EscanearAsync();

        Assert.Equal(3, (await _context.Variantes.SingleAsync()).Stock);
        Assert.Equal(3, await _context.MovimientosStock.CountAsync());
    }

    [Fact]
    public async Task Escanear_SkuConEspaciosOSaltoDeLinea_LoNormaliza()
    {
        await CrearVarianteAsync(stock: 0);

        var resultado = await EscanearAsync("  CAM-NEG-M\n");

        Assert.NotNull(resultado);
        Assert.Equal(1, resultado.Stock);
    }

    [Fact]
    public async Task Escanear_SkuInexistente_DevuelveNullYNoRegistraMovimiento()
    {
        await CrearVarianteAsync(stock: 5);

        var resultado = await EscanearAsync("NO-EXISTE");

        Assert.Null(resultado);
        Assert.Empty(_context.MovimientosStock);
        Assert.Equal(5, (await _context.Variantes.SingleAsync()).Stock);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Escanear_SkuVacio_LanzaArgumentException(string sku)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => EscanearAsync(sku));
    }

    [Fact]
    public async Task Escanear_IdVacio_LanzaArgumentException()
    {
        await CrearVarianteAsync(stock: 0);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RegistrarEscaneoQrAsync(Sku, Guid.Empty));
        Assert.Empty(_context.MovimientosStock);
    }

    // Reintento del lector (p. ej. se perdió la respuesta): mismo id, no suma otra vez.
    [Fact]
    public async Task Reintentar_MismoId_NoDuplicaElIncremento()
    {
        var variante = await CrearVarianteAsync(stock: 2);
        var idEscaneo = Guid.NewGuid();

        var primero = await _service.RegistrarEscaneoQrAsync(Sku, idEscaneo);
        var reintento = await _service.RegistrarEscaneoQrAsync(Sku, idEscaneo);
        var otroReintento = await _service.RegistrarEscaneoQrAsync(" CAM-NEG-M ", idEscaneo);

        Assert.NotNull(primero);
        Assert.False(primero.Duplicado);
        Assert.NotNull(reintento);
        Assert.True(reintento.Duplicado);
        Assert.Equal(3, reintento.Stock);
        Assert.True(otroReintento!.Duplicado);
        Assert.Equal(idEscaneo, reintento.IdEscaneo);
        Assert.Equal(3, (await _context.Variantes.FindAsync(variante.Id))!.Stock);
        Assert.Single(_context.MovimientosStock);
    }

    [Fact]
    public async Task Reintentar_MismoIdConOtroSku_LanzaConflictoYNoModificaStock()
    {
        await CrearVarianteAsync(stock: 0);
        var otra = await CrearVarianteAsync(stock: 7, sku: "CAM-NEG-L", talla: "L");
        var idEscaneo = Guid.NewGuid();
        await _service.RegistrarEscaneoQrAsync(Sku, idEscaneo);

        await Assert.ThrowsAsync<EscaneoQrConflictoException>(
            () => _service.RegistrarEscaneoQrAsync("CAM-NEG-L", idEscaneo));

        Assert.Equal(7, (await _context.Variantes.FindAsync(otra.Id))!.Stock);
        Assert.Single(_context.MovimientosStock);
    }

    [Fact]
    public async Task EscanearYVender_ElStockSubeConElQrYBajaAlConfirmarLaVenta()
    {
        var variante = await CrearVarianteAsync(stock: 0);
        var ventas = new VentasService(_context);

        await EscanearAsync();
        await EscanearAsync();

        var venta = await ventas.CreateAsync(new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto>
            {
                new() { VarianteId = variante.Id, Cantidad = 1, PrecioUnitario = 45000m }
            }
        });
        await ventas.ConfirmarAsync(venta.Id);

        Assert.Equal(1, (await _context.Variantes.FindAsync(variante.Id))!.Stock);
        var tipos = await _context.MovimientosStock.Select(m => m.Tipo).ToListAsync();
        Assert.Equal(2, tipos.Count(t => t == "deteccion_ia"));
        Assert.Single(tipos, t => t == "venta");
    }

    // Un reintento que llega después de la venta no vuelve a sumar ni toca
    // los movimientos de la venta.
    [Fact]
    public async Task ReintentoDespuesDeVender_NoAlteraElStockNiLosMovimientosDeVenta()
    {
        var variante = await CrearVarianteAsync(stock: 0);
        var ventas = new VentasService(_context);
        var idEscaneo = Guid.NewGuid();

        await _service.RegistrarEscaneoQrAsync(Sku, idEscaneo);
        var venta = await ventas.CreateAsync(new CrearVentaDto
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto>
            {
                new() { VarianteId = variante.Id, Cantidad = 1, PrecioUnitario = 45000m }
            }
        });
        await ventas.ConfirmarAsync(venta.Id);

        var reintento = await _service.RegistrarEscaneoQrAsync(Sku, idEscaneo);

        Assert.True(reintento!.Duplicado);
        Assert.Equal(0, reintento.Stock);
        Assert.Equal(0, (await _context.Variantes.FindAsync(variante.Id))!.Stock);
        Assert.Equal(2, await _context.MovimientosStock.CountAsync());
    }
}

// Restricción temporal por red del endpoint (hasta que exista autenticación, #84).
public class RedPermitidaEscaneoQrTests
{
    [Theory]
    [InlineData("10.0.0.5", "10.0.0.5", true)]
    [InlineData("10.0.0.5", "10.0.0.6", false)]
    [InlineData("172.18.0.7", "172.18.0.0/16", true)]
    [InlineData("172.19.0.7", "172.18.0.0/16", false)]
    [InlineData("192.168.1.20", "10.0.0.0/8, 192.168.1.0/24", true)]
    [InlineData("::ffff:172.18.0.7", "172.18.0.0/16", true)]
    [InlineData("10.0.0.5", "no-es-una-ip, 10.0.0.0/33, 172.18.0.5/16", false)]
    [InlineData("10.0.0.5", "", false)]
    [InlineData("127.0.0.1", "", false)]
    public void EsIpPermitida(string ip, string redes, bool esperado)
    {
        Assert.Equal(esperado, RedPermitidaEscaneoQrAttribute.EsIpPermitida(IPAddress.Parse(ip), redes));
    }

    [Fact]
    public void EsIpPermitida_SinIpDeOrigen_Rechaza()
    {
        Assert.False(RedPermitidaEscaneoQrAttribute.EsIpPermitida(null, "0.0.0.0/0"));
    }

    private static AuthorizationFilterContext CrearContexto(string entorno, string? redes, string? ip)
    {
        var servicios = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IHostEnvironment>(new EntornoFalso { EnvironmentName = entorno })
            .AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [RedPermitidaEscaneoQrAttribute.ClaveConfiguracion] = redes
                })
                .Build())
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = servicios };
        if (ip != null)
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(ip);

        return new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>());
    }

    [Fact]
    public void Development_Permite()
    {
        var contexto = CrearContexto(Environments.Development, redes: null, ip: "203.0.113.9");

        new RedPermitidaEscaneoQrAttribute().OnAuthorization(contexto);

        Assert.Null(contexto.Result);
    }

    [Fact]
    public void Produccion_SinRedesConfiguradas_Retorna403()
    {
        var contexto = CrearContexto(Environments.Production, redes: null, ip: "127.0.0.1");

        new RedPermitidaEscaneoQrAttribute().OnAuthorization(contexto);

        var resultado = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, resultado.StatusCode);
    }

    [Fact]
    public void Produccion_IpEnRedPermitida_Permite()
    {
        var contexto = CrearContexto(Environments.Production, redes: "172.18.0.0/16", ip: "172.18.0.3");

        new RedPermitidaEscaneoQrAttribute().OnAuthorization(contexto);

        Assert.Null(contexto.Result);
    }

    [Fact]
    public void Produccion_IpFueraDeRed_Retorna403()
    {
        var contexto = CrearContexto(Environments.Production, redes: "172.18.0.0/16", ip: "203.0.113.9");

        new RedPermitidaEscaneoQrAttribute().OnAuthorization(contexto);

        var resultado = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, resultado.StatusCode);
    }

    private sealed class EntornoFalso : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "ErpModa.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

// Validación del cuerpo del endpoint: estos casos se rechazan antes de tocar la base de datos.
public class EscaneoQrHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string IdValido = "6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public EscaneoQrHttpTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string json)
    {
        using var contenido = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PostAsync("/api/productos/escaneo-qr", contenido);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"sku\":\"\",\"idEscaneo\":\"" + IdValido + "\"}")]
    [InlineData("{\"sku\":\"CAM-NEG-M\",\"idEscaneo\":\"" + IdValido + "\",\"cantidad\":5}")]
    [InlineData("{\"sku\":123,\"idEscaneo\":\"" + IdValido + "\"}")]
    [InlineData("{\"sku\":\"CAM-NEG-M\"}")]
    [InlineData("{\"sku\":\"CAM-NEG-M\",\"idEscaneo\":\"no-es-un-uuid\"}")]
    [InlineData("{\"sku\":\"CAM-NEG-M\",\"idEscaneo\":\"00000000-0000-0000-0000-000000000000\"}")]
    public async Task PostEscaneoQr_CuerpoInvalido_Retorna400(string json)
    {
        using var response = await PostAsync(_client, json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostEscaneoQr_SkuDeMasDe50Caracteres_Retorna400()
    {
        var json = $"{{\"sku\":\"{new string('X', 51)}\",\"idEscaneo\":\"{IdValido}\"}}";

        using var response = await PostAsync(_client, json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("40001", true)]   // serialization_failure
    [InlineData("40P01", true)]   // deadlock_detected
    [InlineData("23505", false)]  // unique_violation: no es un conflicto de serialización
    [InlineData("42P01", false)]  // undefined_table: error permanente
    public void ConflictoDeSerializacion_SoloCodigosTransitorios(string sqlState, bool esperado)
    {
        Assert.Equal(esperado, ProductosService.EsConflictoSerializacion(sqlState));
    }

    // Fuera de Development y sin EscaneoQr:RedesPermitidas, el endpoint no se expone.
    [Fact]
    public async Task PostEscaneoQr_EnProduccionSinRedesPermitidas_Retorna403()
    {
        using var produccion = _factory.WithWebHostBuilder(b => b.UseEnvironment(Environments.Production));
        using var client = produccion.CreateClient();

        using var response = await PostAsync(client, $"{{\"sku\":\"CAM-NEG-M\",\"idEscaneo\":\"{IdValido}\"}}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
