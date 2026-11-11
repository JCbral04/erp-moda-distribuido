using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ErpModa.Api.Inventario.DTOs;

namespace ErpModa.Api.Tests;

// Validación de los DTOs de Inventario (issue #87). [ApiController] responde 400
// cuando falla cualquiera de estas reglas o la deserialización del JSON.
public class ProductosDtosValidacionTests
{
    // Mismas opciones que usa ASP.NET Core para el cuerpo de las peticiones.
    private static readonly JsonSerializerOptions OpcionesWeb = new(JsonSerializerDefaults.Web);

    private static List<ValidationResult> Validar(object dto)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), resultados, validateAllProperties: true);
        return resultados;
    }

    private static CrearVarianteDto VarianteValida(string sku = "CAM-NEG-M", string talla = "M", string color = "Negro") =>
        new() { Talla = talla, Color = color, Sku = sku, Stock = 10, Sobreprecio = null };

    private static CrearProductoDto ProductoValido() => new()
    {
        Nombre = "Camiseta básica",
        Descripcion = "Algodón",
        PrecioBase = 45000m,
        Categoria = "Camisetas",
        Variantes = new List<CrearVarianteDto> { VarianteValida() }
    };

    private static ActualizarProductoDto ActualizacionValida() => new()
    {
        Nombre = "Camiseta básica",
        PrecioBase = 45000m,
        Categoria = "Camisetas",
        Activo = true
    };

    // ── CrearProductoDto ──

    [Fact]
    public void CrearProducto_Valido_NoTieneErrores()
    {
        Assert.Empty(Validar(ProductoValido()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearProducto_NombreVacio_TieneError(string nombre)
    {
        var dto = ProductoValido();
        dto.Nombre = nombre;

        Assert.Contains(Validar(dto), r => r.MemberNames.Contains(nameof(CrearProductoDto.Nombre)));
    }

    [Fact]
    public void CrearProducto_NombreDeMasDe150Caracteres_TieneError()
    {
        var dto = ProductoValido();
        dto.Nombre = new string('a', 151);

        Assert.Contains(Validar(dto), r => r.MemberNames.Contains(nameof(CrearProductoDto.Nombre)));
    }

    [Fact]
    public void CrearProducto_CategoriaVacia_TieneError()
    {
        var dto = ProductoValido();
        dto.Categoria = "";

        Assert.Contains(Validar(dto), r => r.MemberNames.Contains(nameof(CrearProductoDto.Categoria)));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("10000000000")]
    public void CrearProducto_PrecioBaseFueraDeRango_TieneError(string precio)
    {
        var dto = ProductoValido();
        dto.PrecioBase = decimal.Parse(precio, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Contains(Validar(dto), r => r.MemberNames.Contains(nameof(CrearProductoDto.PrecioBase)));
    }

    [Fact]
    public void CrearProducto_SkuRepetidoEnVariantes_TieneError()
    {
        var dto = ProductoValido();
        dto.Variantes = new List<CrearVarianteDto>
        {
            VarianteValida(sku: "SKU-1", talla: "M"),
            VarianteValida(sku: "SKU-1", talla: "L")
        };

        var error = Assert.Single(Validar(dto));
        Assert.Contains("SKU-1", error.ErrorMessage);
    }

    [Fact]
    public void CrearProducto_TallaYColorRepetidosEnVariantes_TieneError()
    {
        var dto = ProductoValido();
        dto.Variantes = new List<CrearVarianteDto>
        {
            VarianteValida(sku: "SKU-1"),
            VarianteValida(sku: "SKU-2")
        };

        var error = Assert.Single(Validar(dto));
        Assert.Contains("talla 'M'", error.ErrorMessage);
    }

    [Fact]
    public void CrearProducto_VarianteNula_TieneError()
    {
        var dto = ProductoValido();
        dto.Variantes = new List<CrearVarianteDto> { VarianteValida(), null! };

        Assert.Single(Validar(dto));
    }

    [Fact]
    public void CrearProducto_SinVariantes_EsValido()
    {
        var dto = ProductoValido();
        dto.Variantes = new List<CrearVarianteDto>();

        Assert.Empty(Validar(dto));
    }

    // ── CrearVarianteDto ──

    [Fact]
    public void CrearVariante_SinSobreprecio_EsValida()
    {
        Assert.Empty(Validar(VarianteValida()));
    }

    [Theory]
    [InlineData(nameof(CrearVarianteDto.Talla), "")]
    [InlineData(nameof(CrearVarianteDto.Color), " ")]
    [InlineData(nameof(CrearVarianteDto.Sku), "")]
    public void CrearVariante_CampoObligatorioVacio_TieneError(string campo, string valor)
    {
        var dto = VarianteValida();
        typeof(CrearVarianteDto).GetProperty(campo)!.SetValue(dto, valor);

        Assert.Contains(Validar(dto), r => r.MemberNames.Contains(campo));
    }

    [Theory]
    [InlineData(nameof(CrearVarianteDto.Talla), 21)]
    [InlineData(nameof(CrearVarianteDto.Color), 51)]
    [InlineData(nameof(CrearVarianteDto.Sku), 51)]
    public void CrearVariante_CampoDemasiadoLargo_TieneError(string campo, int longitud)
    {
        var dto = VarianteValida();
        typeof(CrearVarianteDto).GetProperty(campo)!.SetValue(dto, new string('x', longitud));

        Assert.Contains(Validar(dto), r => r.MemberNames.Contains(campo));
    }

    [Fact]
    public void CrearVariante_StockNegativo_TieneError()
    {
        var dto = VarianteValida();
        dto.Stock = -1;

        Assert.Contains(Validar(dto), r => r.MemberNames.Contains(nameof(CrearVarianteDto.Stock)));
    }

    [Fact]
    public void CrearVariante_SobreprecioNegativo_TieneError()
    {
        var dto = VarianteValida();
        dto.Sobreprecio = -500m;

        Assert.Contains(Validar(dto), r => r.MemberNames.Contains(nameof(CrearVarianteDto.Sobreprecio)));
    }

    // ── ActualizarProductoDto ──

    [Fact]
    public void ActualizarProducto_Valido_NoTieneErrores()
    {
        Assert.Empty(Validar(ActualizacionValida()));
    }

    [Fact]
    public void ActualizarProducto_PrecioCero_TieneError()
    {
        var dto = ActualizacionValida();
        dto.PrecioBase = 0;

        Assert.Contains(Validar(dto), r => r.MemberNames.Contains(nameof(ActualizarProductoDto.PrecioBase)));
    }

    [Fact]
    public void ActualizarProducto_NombreVacio_TieneError()
    {
        var dto = ActualizacionValida();
        dto.Nombre = "";

        Assert.Contains(Validar(dto), r => r.MemberNames.Contains(nameof(ActualizarProductoDto.Nombre)));
    }

    // ── Deserialización del JSON (campos inesperados, tipos erróneos, campos requeridos) ──

    [Fact]
    public void JsonCrearProducto_Valido_SeDeserializa()
    {
        const string json = """
            {"nombre":"Camiseta","precioBase":45000,"categoria":"Camisetas",
             "variantes":[{"talla":"M","color":"Negro","sku":"CAM-1","stock":5}]}
            """;

        var dto = JsonSerializer.Deserialize<CrearProductoDto>(json, OpcionesWeb);

        Assert.NotNull(dto);
        Assert.Equal("CAM-1", dto.Variantes[0].Sku);
    }

    [Fact]
    public void JsonCrearProducto_CampoInesperado_SeRechaza()
    {
        const string json = """{"nombre":"Camiseta","precioBase":45000,"categoria":"Camisetas","activo":false}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CrearProductoDto>(json, OpcionesWeb));
    }

    [Fact]
    public void JsonCrearProducto_CampoInesperadoEnVariante_SeRechaza()
    {
        const string json = """
            {"nombre":"Camiseta","precioBase":45000,"categoria":"Camisetas",
             "variantes":[{"talla":"M","color":"Negro","sku":"CAM-1","stock":5,"id":7}]}
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CrearProductoDto>(json, OpcionesWeb));
    }

    [Fact]
    public void JsonCrearProducto_TipoErroneo_SeRechaza()
    {
        const string json = """{"nombre":"Camiseta","precioBase":"caro","categoria":"Camisetas"}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CrearProductoDto>(json, OpcionesWeb));
    }

    [Fact]
    public void JsonActualizarProducto_SinActivo_SeRechaza()
    {
        const string json = """{"nombre":"Camiseta","precioBase":45000,"categoria":"Camisetas"}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ActualizarProductoDto>(json, OpcionesWeb));
    }

    [Fact]
    public void JsonActualizarProducto_CampoInesperado_SeRechaza()
    {
        const string json = """{"nombre":"Camiseta","precioBase":45000,"categoria":"Camisetas","activo":true,"id":3}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ActualizarProductoDto>(json, OpcionesWeb));
    }
}
