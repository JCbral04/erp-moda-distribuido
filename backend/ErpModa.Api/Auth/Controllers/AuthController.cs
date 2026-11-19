using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ErpModa.Api.Auth.DTOs;
using ErpModa.Api.Auth.Services;
using ErpModa.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ErpModa.Api.Auth.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private const string BootstrapHeader = "X-Bootstrap-Token";
        private readonly IAuthService _auth;
        private readonly string? _bootstrapToken;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService auth, Microsoft.Extensions.Configuration.IConfiguration configuration, ILogger<AuthController> logger)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _bootstrapToken = configuration["Auth:BootstrapToken"];
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Inicialización controlada del PRIMER administrador. Requiere el token
        /// de arranque configurado fuera del código (User Secrets / entorno).
        /// </summary>
        [HttpPost("inicializar")]
        [AllowAnonymous]
        public async Task<IActionResult> Inicializar(
            [FromHeader(Name = BootstrapHeader)] string? token,
            [FromBody] BootstrapAdminRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_bootstrapToken))
            {
                return StatusCode(503, new
                {
                    mensaje = "La inicialización no está habilitada. Configure Auth:BootstrapToken antes de crear el primer administrador."
                });
            }

            if (!Coincide(token, _bootstrapToken))
                return Unauthorized(new { mensaje = "Token de inicialización no válido." });

            try
            {
                var usuario = await _auth.InicializarAdministradorAsync(request, cancellationToken);
                return Ok(usuario);
            }
            catch (InicializacionNoDisponibleException ex)
            {
                return Conflict(new { mensaje = ex.Message });
            }
            catch (UsuarioDuplicadoException ex)
            {
                return Conflict(new { mensaje = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _auth.LoginAsync(request, cancellationToken));
            }
            catch (CredencialesInvalidasException ex)
            {
                _logger.LogWarning("Autenticacion fallida CorrelationId={CorrelationId} Result={Result}",
                    CorrelationIdMiddleware.Get(HttpContext), "401");
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (ConfiguracionAuthInvalidaException ex)
            {
                return StatusCode(500, new { mensaje = ex.Message });
            }
        }

        /// <summary>Creación de usuarios posteriores: solo un administrador autenticado.</summary>
        [HttpPost("registro")]
        [Authorize(Policy = "Administrador")]
        public async Task<IActionResult> Registrar([FromBody] CrearUsuarioRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var usuario = await _auth.CrearUsuarioAsync(request, cancellationToken);
                return CreatedAtAction(nameof(Registrar), new { id = usuario.Id }, usuario);
            }
            catch (UsuarioDuplicadoException ex)
            {
                return Conflict(new { mensaje = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
        }

        private static bool Coincide(string? proporcionado, string esperado)
        {
            if (string.IsNullOrEmpty(proporcionado)) return false;
            var a = Encoding.UTF8.GetBytes(proporcionado);
            var b = Encoding.UTF8.GetBytes(esperado);
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }
    }
}
