using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpModa.Api.Auth.Configuration;
using ErpModa.Api.Auth.DTOs;
using ErpModa.Api.Auth.Models;
using ErpModa.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace ErpModa.Api.Tests;

public sealed class AuthTestFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    public string JwtKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public string BootstrapToken { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public const string Issuer = "ErpModa";
    public const string Audience = "ErpModa.Client";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:Key", JwtKey);
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Auth:BootstrapToken", BootstrapToken);

        builder.ConfigureServices(servicios =>
        {
            // El host de pruebas usa su propio contenedor: se asegura aquí la
            // misma configuración JWT que valida el middleware.
            servicios.Configure<JwtSettings>(opciones =>
            {
                opciones.Key = JwtKey;
                opciones.Issuer = Issuer;
                opciones.Audience = Audience;
            });
            servicios.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, opciones =>
            {
                opciones.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
                opciones.TokenValidationParameters.ValidIssuer = Issuer;
                opciones.TokenValidationParameters.ValidAudience = Audience;
            });
            // EF 9 registra también la configuración de opciones: hay que quitarla
            // para que no convivan los proveedores Npgsql y InMemory.
            servicios.RemoveAll<DbContextOptions<ErpModaDbContext>>();
            servicios.RemoveAll(typeof(IDbContextOptionsConfiguration<ErpModaDbContext>));
            servicios.AddDbContext<ErpModaDbContext>(opciones => opciones.UseInMemoryDatabase(_dbName));
        });
    }

    /// <summary>Token firmado con la clave de prueba para cualquier rol.</summary>
    public string CrearToken(int id, string rol)
    {
        var ahora = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim(ClaimTypes.Email, $"usuario{id}@erp.com"),
                new Claim(ClaimTypes.Role, rol)
            },
            notBefore: ahora.AddMinutes(-1),
            expires: ahora.AddMinutes(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void CrearUsuarioDirecto(string email, string password, RolUsuario rol)
    {
        using var ambito = Services.CreateScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ErpModaDbContext>();
        contexto.Usuarios.Add(new Usuario
        {
            Email = Usuario.NormalizarEmail(email),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Rol = rol,
            CreadoEn = DateTime.UtcNow,
            ActualizadoEn = DateTime.UtcNow
        });
        contexto.SaveChanges();
    }

    public bool ExisteUsuario(string email)
    {
        using var ambito = Services.CreateScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ErpModaDbContext>();
        return contexto.Usuarios.Any(u => u.Email == Usuario.NormalizarEmail(email));
    }

    public string? ObtenerHash(string email)
    {
        using var ambito = Services.CreateScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ErpModaDbContext>();
        return contexto.Usuarios.AsNoTracking()
            .FirstOrDefault(u => u.Email == Usuario.NormalizarEmail(email))?.PasswordHash;
    }
}

public class AuthTests
{
    private const string Clave = "ClaveDePrueba123!";

    private static StringContent Json(object cuerpo) =>
        new(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json");

    private static async Task<LoginResponse?> LeerLogin(HttpResponseMessage respuesta)
    {
        var texto = await respuesta.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<LoginResponse>(texto, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static string CrearToken(
        string clave,
        string emisor,
        string audiencia,
        IEnumerable<Claim> claims,
        DateTime? expira = null)
    {
        var antes = expira.HasValue && expira.Value < DateTime.UtcNow
            ? expira.Value.AddMinutes(-10)
            : DateTime.UtcNow.AddMinutes(-1);
        var token = new JwtSecurityToken(
            issuer: emisor,
            audience: audiencia,
            claims: claims,
            notBefore: antes,
            expires: expira ?? DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static IEnumerable<Claim> ClaimsValidos(int id = 1, string rol = "Administrador") => new[]
    {
        new Claim(ClaimTypes.NameIdentifier, id.ToString()),
        new Claim(ClaimTypes.Email, "admin@erp.com"),
        new Claim(ClaimTypes.Role, rol)
    };

    /// <summary>Endpoint real protegido con la política "Administrador".</summary>
    private static Task<HttpResponseMessage> RegistroProtegido(
        HttpClient cliente, string token, string email = "nuevo@erp.com") =>
        cliente.PostAsync("/api/auth/registro",
            Json(new { email, password = Clave, rol = (int)RolUsuario.Vendedor }));

    [Fact]
    public async Task Login_ConCredencialesCorrectas_DevuelveTokenValido()
    {
        using var fabrica = new AuthTestFactory();
        fabrica.CrearUsuarioDirecto("Admin@ERP.com", Clave, RolUsuario.Administrador);
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.PostAsync("/api/auth/login", Json(new { email = "admin@erp.com", password = Clave }));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var login = await LeerLogin(respuesta);
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login!.Token));
        Assert.Equal("Administrador", login.Usuario.Rol);
        Assert.DoesNotContain("passwordHash", await respuesta.Content.ReadAsStringAsync());
        // La expiración declarada es de 60 minutos.
        Assert.InRange(login.ExpiraEn, DateTime.UtcNow.AddMinutes(59), DateTime.UtcNow.AddMinutes(61));

        // El token emitido debe satisfacer la política "Administrador" en un endpoint real.
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        var registro = await RegistroProtegido(cliente, login.Token);
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        Assert.True(fabrica.ExisteUsuario("nuevo@erp.com"));
    }

    [Fact]
    public async Task Login_ConCorreoONoContrasenaIncorrectos_Devuelve401MismoMensaje()
    {
        using var fabrica = new AuthTestFactory();
        fabrica.CrearUsuarioDirecto("admin@erp.com", Clave, RolUsuario.Administrador);
        var cliente = fabrica.CreateClient();

        var passwordIncorrecta = await cliente.PostAsync("/api/auth/login",
            Json(new { email = "admin@erp.com", password = "OtraClave123!" }));
        var correoInexistente = await cliente.PostAsync("/api/auth/login",
            Json(new { email = "nadie@erp.com", password = Clave }));

        Assert.Equal(HttpStatusCode.Unauthorized, passwordIncorrecta.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, correoInexistente.StatusCode);
        Assert.Equal(await passwordIncorrecta.Content.ReadAsStringAsync(),
                     await correoInexistente.Content.ReadAsStringAsync());
        Assert.Contains("Credenciales inválidas.", await passwordIncorrecta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EndpointProtegido_SinToken_Devuelve401()
    {
        using var fabrica = new AuthTestFactory();
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.PostAsync("/api/auth/registro",
            Json(new { email = "nuevo@erp.com", password = Clave, rol = (int)RolUsuario.Vendedor }));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task TokenConFirmaInvalida_Devuelve401()
    {
        using var fabrica = new AuthTestFactory();
        var cliente = fabrica.CreateClient();
        var otraClave = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var token = CrearToken(otraClave, AuthTestFactory.Issuer, AuthTestFactory.Audience, ClaimsValidos());

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var respuesta = await cliente.PostAsync("/api/auth/registro",
            Json(new { email = "nuevo@erp.com", password = Clave, rol = (int)RolUsuario.Vendedor }));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task TokenExpirado_Devuelve401()
    {
        using var fabrica = new AuthTestFactory();
        var cliente = fabrica.CreateClient();
        var token = CrearToken(fabrica.JwtKey, AuthTestFactory.Issuer, AuthTestFactory.Audience,
            ClaimsValidos(), DateTime.UtcNow.AddMinutes(-5));

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var respuesta = await cliente.PostAsync("/api/auth/registro",
            Json(new { email = "nuevo@erp.com", password = Clave, rol = (int)RolUsuario.Vendedor }));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task TokenSinClaimDeRol_Devuelve401()
    {
        using var fabrica = new AuthTestFactory();
        var cliente = fabrica.CreateClient();
        var token = CrearToken(fabrica.JwtKey, AuthTestFactory.Issuer, AuthTestFactory.Audience,
            new[] { new Claim(ClaimTypes.NameIdentifier, "1") });

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var respuesta = await cliente.PostAsync("/api/auth/registro",
            Json(new { email = "nuevo@erp.com", password = Clave, rol = (int)RolUsuario.Vendedor }));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Inicializar_CreaPrimerAdministradorConHashBCrypt()
    {
        using var fabrica = new AuthTestFactory();
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Bootstrap-Token", fabrica.BootstrapToken);

        var respuesta = await cliente.PostAsync("/api/auth/inicializar",
            Json(new { email = "FUNDADOR@ERP.COM", password = Clave }));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("Administrador", cuerpo);
        Assert.DoesNotContain("passwordHash", cuerpo);
        Assert.True(fabrica.ExisteUsuario("fundador@erp.com"));
        Assert.StartsWith("$2", fabrica.ObtenerHash("fundador@erp.com"));

        var login = await cliente.PostAsync("/api/auth/login",
            Json(new { email = "fundador@erp.com", password = Clave }));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Inicializar_SinTokenDeArranque_NoCreaUsuario()
    {
        using var fabrica = new AuthTestFactory();
        var cliente = fabrica.CreateClient();

        var respuesta = await cliente.PostAsync("/api/auth/inicializar",
            Json(new { email = "fundador@erp.com", password = Clave }));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.False(fabrica.ExisteUsuario("fundador@erp.com"));
    }

    [Fact]
    public async Task Inicializar_CuandoYaExisteUsuario_Devuelve409()
    {
        using var fabrica = new AuthTestFactory();
        fabrica.CrearUsuarioDirecto("admin@erp.com", Clave, RolUsuario.Administrador);
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Bootstrap-Token", fabrica.BootstrapToken);

        var respuesta = await cliente.PostAsync("/api/auth/inicializar",
            Json(new { email = "otro@erp.com", password = Clave }));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task RegistroDuplicado_ConTokenDeAdministrador_Devuelve409()
    {
        using var fabrica = new AuthTestFactory();
        fabrica.CrearUsuarioDirecto("admin@erp.com", Clave, RolUsuario.Administrador);
        var token = CrearToken(fabrica.JwtKey, AuthTestFactory.Issuer, AuthTestFactory.Audience, ClaimsValidos());
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await cliente.PostAsync("/api/auth/registro",
            Json(new { email = "ADMIN@ERP.COM", password = Clave, rol = (int)RolUsuario.Vendedor }));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Registro_ConTokenDeVendedor_Devuelve403()
    {
        using var fabrica = new AuthTestFactory();
        fabrica.CrearUsuarioDirecto("vendedor@erp.com", Clave, RolUsuario.Vendedor);
        var token = CrearToken(fabrica.JwtKey, AuthTestFactory.Issuer, AuthTestFactory.Audience,
            ClaimsValidos(2, "Vendedor"));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await cliente.PostAsync("/api/auth/registro",
            Json(new { email = "nuevo@erp.com", password = Clave, rol = (int)RolUsuario.Administrador }));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.False(fabrica.ExisteUsuario("nuevo@erp.com"));
    }
}
