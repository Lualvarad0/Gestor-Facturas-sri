using Back.Dtos;
using Back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Back.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/[controller]")]
    [EnableRateLimiting("auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request) =>
            Ok(await _authService.RegistrarAsync(request));

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request) =>
            Ok(await _authService.LoginAsync(request));

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
        {
            await _authService.SolicitarRestablecimientoAsync(request);
            return Ok(new { mensaje = "Si existe una cuenta con ese correo, recibirás un enlace para restablecer tu contraseña." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            await _authService.RestablecerAsync(request);
            return Ok(new { mensaje = "Contraseña actualizada. Inicia sesión con tu nueva contraseña." });
        }
    }
}
