using System.ComponentModel.DataAnnotations;
using Back.Dtos;
using Back.Helpers;
using Back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Back.Controllers
{
    /// <summary>Presupuesto del usuario: ingreso mensual y rubros propios con su límite.</summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class PresupuestoController : ControllerBase
    {
        private readonly PresupuestoService _presupuestoService;

        public PresupuestoController(PresupuestoService presupuestoService)
        {
            _presupuestoService = presupuestoService;
        }

        private int UsuarioId => JwtHelper.GetUsuarioId(User);

        // GET api/presupuesto?anio=2026&mes=10  (gasto de cada rubro en ese mes)
        [HttpGet]
        public async Task<ActionResult<PresupuestoDto>> GetPresupuesto(
            [FromQuery, Required, Range(2000, 2100)] int anio,
            [FromQuery, Required, Range(1, 12)] int mes) =>
            Ok(await _presupuestoService.ObtenerAsync(UsuarioId, anio, mes));

        [HttpPut]
        public async Task<IActionResult> PutPresupuesto(PresupuestoRequest request)
        {
            await _presupuestoService.GuardarAsync(UsuarioId, request);
            return NoContent();
        }

        [HttpPut("proveedores/{ruc}/rubro")]
        public async Task<IActionResult> AsignarRubroProveedor(
            [RegularExpression(@"^\d{13}$")] string ruc,
            [FromBody] AsignarRubroRequest request)
        {
            await _presupuestoService.AsignarProveedorAsync(UsuarioId, ruc, request.RubroId);
            return NoContent();
        }
    }
}
