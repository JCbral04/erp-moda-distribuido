using System.Net;
using System.Text;
using ErpModa.Api.Data;
using ErpModa.Api.Inventario.Models;
using ErpModa.Api.Inventario.Services;
using ErpModa.Api.Ventas.DTOs;
using ErpModa.Api.Ventas.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

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

    private async Task<Variante> CrearVarianteAsync(int stock)
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
            Talla = "M",
            Color = "Negro",
            Sku = Sku,
            Stock = stock
        };
        _context.Productos.Add(producto);
        _context.Variantes.Add(variante);
        await _context.SaveChangesAsync();
        return variante;
    }

    [Fact]
    public async Task Escanear_SkuExistente_SumaUnaUnidad()
    {
        var variante = await CrearVarianteAsync(stock: 3);

        var resultado = await _service.RegistrarEscaneoQrAsync(Sku);

        Assert.NotNull(resultado);
        Assert.Equal(variante.Id, resultado.VarianteId);
        Assert.Equal(4, resultado.Stock);
        Assert.Equal("Camiseta básica", resultado.Producto);
        Assert.Equal(4, (await _context.Variantes.FindAsync(variante.Id))!.Stock);
    }

    [Fact]
    public async Task Escanear_RegistraMovimientoDeteccionIa()
    {
        var variante = await CrearVarianteAsync(stock: 0);

        await _service.RegistrarEscaneoQrAsync(Sku);

        var movimiento = Assert.Single(_context.MovimientosStock);
        Assert.Equal(variante.Id, movimiento.VarianteId);
        Assert.Equal("deteccion_ia", movimiento.Tipo);
        Assert.Equal(1, movimiento.Cantidad);
        Assert.Null(movimiento.ReferenciaTipo);
        Assert.Equal(1m, movimiento.Confianza);
    }

    [Fact]
    public async Task Escanear_VariasVeces_SumaCadaEscaneo()
    {
        await CrearVarianteAsync(stock: 0);

        for (var i = 0; i < 3; i++)
            await _service.RegistrarEscaneoQrAsync(Sku);

        Assert.Equal(3, (await _context.Variantes.SingleAsync()).Stock);
        Assert.Equal(3, await _context.MovimientosStock.CountAsync());
    }

    [Fact]
    public async Task Escanear_SkuConEspaciosOSaltoDeLinea_LoNormaliza()
    {
        await CrearVarianteAsync(stock: 0);

        var resultado = await _service.RegistrarEscaneoQrAsync("  CAM-NEG-M\n");

        Assert.NotNull(resultado);
        Assert.Equal(1, resultado.Stock);
    }

    [Fact]
    public async Task Escanear_SkuInexistente_DevuelveNullYNoRegistraMovimiento()
    {
        await CrearVarianteAsync(stock: 5);

        var resultado = await _service.RegistrarEscaneoQrAsync("NO-EXISTE");

        Assert.Null(resultado);
        Assert.Empty(_context.MovimientosStock);
        Assert.Equal(5, (await _context.Variantes.SingleAsync()).Stock);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Escanear_SkuVacio_LanzaArgumentException(string sku)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.RegistrarEscaneoQrAsync(sku));
    }

    [Fact]
    public async Task EscanearYVender_ElStockSubeConElQrYBajaAlConfirmarLaVenta()
    {
        var variante = await CrearVarianteAsync(stock: 0);
        var ventas = new VentasService(_context);

        await _service.RegistrarEscaneoQrAsync(Sku);
        await _service.RegistrarEscaneoQrAsync(Sku);

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
}

// Validación del cuerpo del endpoint: estos casos se rechazan antes de tocar la base de datos.
public class EscaneoQrHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EscaneoQrHttpTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"sku\":\"\"}")]
    [InlineData("{\"sku\":\"CAM-NEG-M\",\"cantidad\":5}")]
    [InlineData("{\"sku\":123}")]
    public async Task PostEscaneoQr_CuerpoInvalido_Retorna400(string json)
    {
        var response = await _client.PostAsync("/api/productos/escaneo-qr",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostEscaneoQr_SkuDeMasDe50Caracteres_Retorna400()
    {
        var json = $"{{\"sku\":\"{new string('X', 51)}\"}}";

        var response = await _client.PostAsync("/api/productos/escaneo-qr",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
