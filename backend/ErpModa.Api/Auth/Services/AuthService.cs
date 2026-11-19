using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ErpModa.Api.Auth.Configuration;
using ErpModa.Api.Auth.DTOs;
using ErpModa.Api.Auth.Models;
using ErpModa.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace ErpModa.Api.Auth.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

        Task<UsuarioResponse> InicializarAdministradorAsync(
            BootstrapAdminRequest request, CancellationToken cancellationToken = default);

        Task<UsuarioResponse> CrearUsuarioAsync(
            CrearUsuarioRequest request, CancellationToken cancellationToken = default);
    }

    public class AuthService : IAuthService
    {
        private readonly ErpModaDbContext _context;
        private readonly JwtSettings _jwt;

        public AuthService(ErpModaDbContext context, IOptions<JwtSettings> jwt)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _jwt = jwt?.Value ?? throw new ArgumentNullException(nameof(jwt));
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        {
            var email = Usuario.NormalizarEmail(request.Email);
            var usuario = await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

            // Mismo mensaje para correo inexistente y contraseña errónea: no se
            // revela si una cuenta existe.
            if (usuario is null || !BCrypt.Net.BCrypt.Verify(request.Password, usuario.PasswordHash))
                throw new CredencialesInvalidasException();

            var expiraEn = DateTime.UtcNow.AddMinutes(_jwt.DuracionMinutos);
            var token = GenerarToken(usuario, expiraEn);

            return new LoginResponse
            {
                Token = token,
                ExpiraEn = expiraEn,
                Usuario = Map(usuario)
            };
        }

        public async Task<UsuarioResponse> InicializarAdministradorAsync(
            BootstrapAdminRequest request, CancellationToken cancellationToken = default)
        {
            if (await _context.Usuarios.AnyAsync(cancellationToken))
                throw new InicializacionNoDisponibleException(
                    "Ya existen usuarios. Cree nuevos usuarios con /api/auth/registro usando un token de administrador.");

            return await CrearAsync(request.Email, request.Password, RolUsuario.Administrador, cancellationToken);
        }

        public async Task<UsuarioResponse> CrearUsuarioAsync(
            CrearUsuarioRequest request, CancellationToken cancellationToken = default)
        {
            if (!Enum.IsDefined(request.Rol))
                throw new ArgumentException("Rol no válido.", nameof(request));

            return await CrearAsync(request.Email, request.Password, request.Rol, cancellationToken);
        }

        private async Task<UsuarioResponse> CrearAsync(
            string email, string password, RolUsuario rol, CancellationToken cancellationToken)
        {
            var normalizado = Usuario.NormalizarEmail(email);
            if (normalizado.Length == 0 || password.Length < 8)
                throw new ArgumentException("Correo y contraseña (mínimo 8 caracteres) son obligatorios.");

            if (await _context.Usuarios.AnyAsync(u => u.Email == normalizado, cancellationToken))
                throw new UsuarioDuplicadoException();

            var ahora = DateTime.UtcNow;
            var usuario = new Usuario
            {
                Email = normalizado,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Rol = rol,
                CreadoEn = ahora,
                ActualizadoEn = ahora
            };
            _context.Usuarios.Add(usuario);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (EsConflictoUnico(ex))
            {
                // Carrera entre dos peticiones concurrentes con el mismo correo.
                throw new UsuarioDuplicadoException();
            }

            return Map(usuario);
        }

        private static bool EsConflictoUnico(DbUpdateException ex) =>
            ex.InnerException is PostgresException { SqlState: "23505" };

        private string GenerarToken(Usuario usuario, DateTime expiraEn)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
            if (key.KeySize < 256)
                throw new ConfiguracionAuthInvalidaException("Jwt:Key debe tener al menos 32 caracteres.");

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Rol.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expiraEn,
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static UsuarioResponse Map(Usuario usuario) => new()
        {
            Id = usuario.Id,
            Email = usuario.Email,
            Rol = usuario.Rol.ToString()
        };
    }
}
