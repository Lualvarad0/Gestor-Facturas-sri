using Back.Dtos;
using Back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Back.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/[controller]")]
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
    }
}
