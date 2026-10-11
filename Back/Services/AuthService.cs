using Back.Dtos;
using Back.Helpers;
using Back.Interfaces;
using Back.Models;
using Microsoft.Extensions.Options;

namespace Back.Services
{
    public class AuthService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly JwtOptions _jwtOptions;

        public AuthService(IUsuarioRepository usuarioRepository, IOptions<JwtOptions> jwtOptions)
        {
            _usuarioRepository = usuarioRepository;
            _jwtOptions = jwtOptions.Value;
        }

        public async Task<AuthResponse> RegistrarAsync(RegisterRequest request)
        {
            var correo = request.Email.Trim().ToLowerInvariant();
            if (await _usuarioRepository.GetByCorreoAsync(correo) is not null)
                throw BusinessException.Conflict("Ya existe una cuenta con ese correo.");

            var usuario = new Usuario
            {
                Nombre = request.Username.Trim(),
                Correo = correo,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                // El primer usuario del sistema queda como administrador.
                Rol = await _usuarioRepository.AnyAsync() ? Roles.User : Roles.Admin,
            };
            await _usuarioRepository.AddAsync(usuario);

            return CrearRespuesta(usuario);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var usuario = await _usuarioRepository.GetByCorreoAsync(request.Email.Trim().ToLowerInvariant());
            if (usuario is null || !BCrypt.Net.BCrypt.Verify(request.Password, usuario.PasswordHash))
                throw new BusinessException("Correo o contraseña incorrectos.", StatusCodes.Status401Unauthorized);

            return CrearRespuesta(usuario);
        }

        private AuthResponse CrearRespuesta(Usuario usuario)
        {
            var (token, expiraEn) = JwtHelper.GenerateToken(_jwtOptions, usuario);
            return new AuthResponse(token, expiraEn, UsuarioDto.From(usuario));
        }
    }
}
