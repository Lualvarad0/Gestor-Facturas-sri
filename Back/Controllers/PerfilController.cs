using Back.Dtos;
using Back.Helpers;
using Back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Back.Controllers
{
    /// <summary>Perfil tributario del usuario autenticado (cédula, RUC, régimen, cargas familiares).</summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class PerfilController : ControllerBase
    {
        private readonly PerfilService _perfilService;

        public PerfilController(PerfilService perfilService)
        {
            _perfilService = perfilService;
        }

        [HttpGet]
        public async Task<ActionResult<PerfilDto>> GetPerfil() =>
            Ok(await _perfilService.ObtenerAsync(JwtHelper.GetUsuarioId(User)));

        /// <summary>Guarda el perfil. Si cambia el RUC, reclasifica los comprobantes en ingresos y gastos.</summary>
        [HttpPut]
        public async Task<ActionResult<PerfilActualizadoDto>> PutPerfil(PerfilRequest request) =>
            Ok(await _perfilService.ActualizarAsync(JwtHelper.GetUsuarioId(User), request));
    }
}
