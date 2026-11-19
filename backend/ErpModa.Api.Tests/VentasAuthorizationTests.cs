using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ErpModa.Api.Data;
using ErpModa.Api.Inventario.Models;
using ErpModa.Api.Ventas.DTOs;
using ErpModa.Api.Ventas.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ErpModa.Api.Tests;

/// <summary>
/// Capa HTTP de #83: identidad JWT -> IVentasService -> ownership.
/// Los escenarios de regla de negocio (stock, factura, anulación) ya están cubiertos
/// en VentasServiceTests; aquí solo se comprueba autorización y propagación del claim.
/// </summary>
public class VentasAuthorizationTests
{
    private static readonly JsonSerializerOptions OpcionesJson = new() { PropertyNameCaseInsensitive = true };

    private static StringContent Json(object cuerpo) =>
        new(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json");

    private static void Autenticar(HttpClient cliente, AuthTestFactory fabrica, int id, string rol) =>
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", fabrica.CrearToken(id, rol));

    /// <summary>Siembra stock y dos ventas: una del vendedor "7" y otra del "8".</summary>
    private static async Task<(int venta7, int venta8)> SembrarAsync(AuthTestFactory fabrica)
    {
        using var ambito = fabrica.Services.CreateScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ErpModaDbContext>();
        var producto = new Producto { Nombre = "P", Categoria = "C", PrecioBase = 10, Activo = true };
        var variante = new Variante
        {
            Producto = producto, Talla = "M", Color = "Negro", Sku = "SKU-AUTH", Stock = 100
        };
        contexto.Productos.Add(producto);
        contexto.Variantes.Add(variante);
        await contexto.SaveChangesAsync();

        var servicio = ambito.ServiceProvider.GetRequiredService<IVentasService>();
        CrearVentaDto NuevaVenta() => new()
        {
            MetodoPago = "Efectivo",
            Detalles = new List<DetalleCrearDto>
            {
                new() { VarianteId = variante.Id, Cantidad = 1, PrecioUnitario = 10 }
            }
        };

        var propia = await servicio.CreateAsync(NuevaVenta(), "7");
        var ajena = await servicio.CreateAsync(NuevaVenta(), "8");
        return (propia.Id, ajena.Id);
    }

    [Fact]
    public async Task Ventas_SinToken_Devuelve401EnTodosLosEndpoints()
    {
        using var fabrica = new AuthTestFactory();
        var cliente = fabrica.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/Ventas")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/Ventas/resumen")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/Ventas/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await cliente.PostAsync("/api/Ventas", Json(new { }))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await cliente.PostAsync("/api/Ventas/1/confirmar", Json(new { }))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await cliente.PostAsync("/api/Ventas/1/anular", Json(new { motivo = "x" }))).StatusCode);
    }

    [Fact]
    public async Task Ventas_AlmacenistaSinPermiso_Devuelve403()
    {
        using var fabrica = new AuthTestFactory();
        var (venta7, _) = await SembrarAsync(fabrica);
        var cliente = fabrica.CreateClient();
        Autenticar(cliente, fabrica, 3, "Almacenista"); // rol válido, sin política de escritura

        var crear = await cliente.PostAsync("/api/Ventas", Json(new { }));
        var confirmar = await cliente.PostAsync($"/api/Ventas/{venta7}/confirmar", Json(new { }));
        var anular = await cliente.PostAsync($"/api/Ventas/{venta7}/anular", Json(new { motivo = "x" }));
        var resumen = await cliente.GetAsync("/api/Ventas/resumen");

        Assert.Equal(HttpStatusCode.Forbidden, crear.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, confirmar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, anular.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, resumen.StatusCode);
        // Sí puede consultar ventas (política VentasConsulta).
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/Ventas")).StatusCode);
    }

    [Fact]
    public async Task Ventas_VendedorSoloVeSusVentasYAdministradorVeTodas()
    {
        using var fabrica = new AuthTestFactory();
        var (venta7, venta8) = await SembrarAsync(fabrica);

        var vendedor = fabrica.CreateClient();
        Autenticar(vendedor, fabrica, 7, "Vendedor");
        var listado = await vendedor.GetAsync("/api/Ventas");
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        var delVendedor = JsonSerializer.Deserialize<List<VentaResponseDto>>(
            await listado.Content.ReadAsStringAsync(), OpcionesJson)!;
        Assert.Single(delVendedor);
        Assert.Equal(venta7, delVendedor[0].Id);

        Assert.Equal(HttpStatusCode.OK, (await vendedor.GetAsync($"/api/Ventas/{venta7}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await vendedor.GetAsync($"/api/Ventas/{venta8}")).StatusCode);

        var administrador = fabrica.CreateClient();
        Autenticar(administrador, fabrica, 1, "Administrador");
        var listadoAdmin = await administrador.GetAsync("/api/Ventas");
        Assert.Equal(HttpStatusCode.OK, listadoAdmin.StatusCode);
        var delAdmin = JsonSerializer.Deserialize<List<VentaResponseDto>>(
            await listadoAdmin.Content.ReadAsStringAsync(), OpcionesJson)!;
        Assert.Contains(delAdmin, v => v.Id == venta7);
        Assert.Contains(delAdmin, v => v.Id == venta8);
        Assert.Equal(HttpStatusCode.OK, (await administrador.GetAsync($"/api/Ventas/{venta8}")).StatusCode);
    }

    [Fact]
    public async Task Ventas_VendedorNoOperaVentaAjenaYRegistraSuIdentidadEnMovimientos()
    {
        using var fabrica = new AuthTestFactory();
        var (venta7, venta8) = await SembrarAsync(fabrica);
        var cliente = fabrica.CreateClient();
        Autenticar(cliente, fabrica, 7, "Vendedor");

        // Intento de operar una venta de otro vendedor: se oculta como 404.
        Assert.Equal(HttpStatusCode.NotFound,
            (await cliente.PostAsync($"/api/Ventas/{venta8}/confirmar", Json(new { }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await cliente.PostAsync($"/api/Ventas/{venta8}/anular", Json(new { motivo = "x" }))).StatusCode);

        // La venta propia sí se opera con el claim del token.
        Assert.Equal(HttpStatusCode.OK,
            (await cliente.PostAsync($"/api/Ventas/{venta7}/confirmar", Json(new { }))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await cliente.PostAsync($"/api/Ventas/{venta7}/anular", Json(new { motivo = "prueba" }))).StatusCode);

        using var ambito = fabrica.Services.CreateScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ErpModaDbContext>();
        var movimientos = await contexto.MovimientosStock
            .Where(m => m.ReferenciaTipo == "venta" && m.ReferenciaId == venta7)
            .ToListAsync();

        Assert.Equal(2, movimientos.Count);
        Assert.All(movimientos, m => Assert.Equal("7", m.Usuario));
        Assert.Contains(movimientos, m => m.Tipo == "venta" && m.Cantidad < 0);
        Assert.Contains(movimientos, m => m.Tipo == "anulacion_venta" && m.Cantidad > 0);
    }
}
