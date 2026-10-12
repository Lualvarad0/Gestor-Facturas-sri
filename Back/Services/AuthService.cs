using Back.Dtos;
using Back.Helpers;
using Back.Interfaces;
using Back.Models;
using Microsoft.Extensions.Options;
using Back.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Back.Services
{
    public class AuthService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly JwtOptions _jwtOptions;
        private readonly AppDbContext _db;
        private readonly IRecoveryMail _mail;
        private readonly ILogger<AuthService> _logger;
        private static readonly string DummyHash = PasswordSecurity.Hash("dummy password for constant work");

        public AuthService(IUsuarioRepository usuarioRepository, IOptions<JwtOptions> jwtOptions,
            AppDbContext db, IRecoveryMail mail, ILogger<AuthService> logger)
        {
            _usuarioRepository = usuarioRepository;
            _jwtOptions = jwtOptions.Value;
            _db = db;
            _mail = mail;
            _logger = logger;
        }

        public async Task<AuthResponse> RegistrarAsync(RegisterRequest request)
        {
            var correo = request.Email.Trim().ToLowerInvariant();
            if (await _usuarioRepository.GetByCorreoAsync(correo) is not null)
                throw BusinessException.Conflict("No se pudo crear la cuenta. Intenta iniciar sesión o recuperar tu contraseña.");

            var (nombres, apellidos) = (request.Nombres.Trim(), request.Apellidos.Trim());
            var usuario = new Usuario
            {
                Nombre = Usuario.NombreCompleto(nombres, apellidos),
                Nombres = nombres,
                Apellidos = apellidos,
                FechaNacimiento = request.FechaNacimiento,
                Correo = correo,
                PasswordHash = PasswordSecurity.Hash(request.Password),
                Rol = Roles.User,
            };
            await _usuarioRepository.AddAsync(usuario);

            return CrearRespuesta(usuario);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var usuario = await _usuarioRepository.GetByCorreoAsync(request.Email.Trim().ToLowerInvariant());
            var valid = PasswordSecurity.Verify(request.Password, usuario?.PasswordHash ?? DummyHash);
            var now = DateTime.UtcNow;
            if (usuario is null || usuario.LockoutUntil > now || !valid)
            {
                if (usuario is not null && !(usuario.LockoutUntil > now))
                {
                    // Incremento atómico: no se pierden intentos cuando llegan solicitudes simultáneas.
                    await _db.Usuarios.Where(u => u.Id == usuario.Id && (u.LockoutUntil == null || u.LockoutUntil <= now)).ExecuteUpdateAsync(s => s
                        .SetProperty(u => u.FailedLoginAttempts, u => u.LockoutUntil != null ? 1 : u.FailedLoginAttempts + 1)
                        .SetProperty(u => u.LockoutUntil, u => u.LockoutUntil != null ? (DateTime?)null
                            : u.FailedLoginAttempts >= 4 ? now.AddMinutes(15) : (DateTime?)null));
                }
                throw new BusinessException("Correo o contraseña incorrectos.", StatusCodes.Status401Unauthorized);
            }

            // Condición adicional para no aceptar una clave vieja durante un restablecimiento concurrente.
            var passwordHash = usuario.PasswordHash.StartsWith("$2")
                ? PasswordSecurity.Hash(request.Password) : usuario.PasswordHash;
            var accepted = await _db.Usuarios.Where(u => u.Id == usuario.Id && u.PasswordHash == usuario.PasswordHash
                && (u.LockoutUntil == null || u.LockoutUntil <= now)).ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.FailedLoginAttempts, 0).SetProperty(u => u.LockoutUntil, (DateTime?)null)
                    .SetProperty(u => u.PasswordHash, passwordHash));
            if (accepted != 1) throw new BusinessException("Correo o contraseña incorrectos.", StatusCodes.Status401Unauthorized);
            usuario.PasswordHash = passwordHash;

            return CrearRespuesta(usuario);
        }

        public async Task SolicitarRestablecimientoAsync(ForgotPasswordRequest request)
        {
            if (!_mail.Configured)
                throw new BusinessException("La recuperación por correo todavía no está disponible. Inténtalo más tarde.", 503);
            var now = DateTime.UtcNow;
            var email = request.Email.Trim().ToLowerInvariant();
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var hash = PasswordSecurity.Fingerprint(token);
            var changed = await _db.Usuarios.Where(u => u.Correo == email
                && (u.ResetRequestedAt == null || u.ResetRequestedAt < now.AddMinutes(-2)))
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.ResetTokenHash, hash)
                    .SetProperty(u => u.ResetExpiresAt, now.AddMinutes(15))
                    .SetProperty(u => u.ResetRequestedAt, now));
            if (changed == 1 && !_mail.Enqueue(email, token))
                _logger.LogWarning("La cola de recuperación está llena.");
        }

        public async Task RestablecerAsync(ResetPasswordRequest request)
        {
            var hash = PasswordSecurity.Fingerprint(request.Token.ToLowerInvariant());
            var email = request.Email.Trim().ToLowerInvariant();
            // Con un enlace vigente, la nueva clave no puede ser la actual ni contener datos personales.
            var usuario = await _db.Usuarios.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Correo == email && u.ResetTokenHash == hash && u.ResetExpiresAt > DateTime.UtcNow)
                ?? throw new BusinessException("El enlace no es válido o ha caducado. Solicita uno nuevo.");
            if (PasswordSecurity.Verify(request.Password, usuario.PasswordHash))
                throw new BusinessException("La nueva contraseña debe ser distinta a la actual.");
            if (PasswordSecurity.ContieneDatosPersonales(request.Password, usuario.Nombres, usuario.Apellidos, usuario.Correo, usuario.FechaNacimiento))
                throw new BusinessException(DatosPersonales.MensajeClave);
            var passwordHash = PasswordSecurity.Hash(request.Password);
            // Actualización condicional atómica: solo una solicitud puede consumir el enlace.
            var changed = await _db.Usuarios.Where(u => u.Correo == email && u.ResetTokenHash == hash
                && u.ResetExpiresAt > DateTime.UtcNow).ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.PasswordHash, passwordHash)
                    .SetProperty(u => u.ResetTokenHash, (string?)null)
                    .SetProperty(u => u.ResetExpiresAt, (DateTime?)null)
                    .SetProperty(u => u.FailedLoginAttempts, 0)
                    .SetProperty(u => u.LockoutUntil, (DateTime?)null));
            if (changed != 1) throw new BusinessException("El enlace no es válido o ha caducado. Solicita uno nuevo.");
        }

        private AuthResponse CrearRespuesta(Usuario usuario)
        {
            var (token, expiraEn) = JwtHelper.GenerateToken(_jwtOptions, usuario);
            return new AuthResponse(token, expiraEn, UsuarioDto.From(usuario));
        }
    }
}
