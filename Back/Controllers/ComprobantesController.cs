using System.ComponentModel.DataAnnotations;
using System.Text;
using Back.Dtos;
using Back.Helpers;
using Back.Interfaces;
using Back.Models;
using Back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Back.Controllers
{
    /// <summary>Comprobantes recibidos (gastos personales) importados desde el SRI.</summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ComprobantesController : ControllerBase
    {
        private const long MaxBytesArchivos = 10 * 1024 * 1024;

        private readonly ComprobanteService _comprobanteService;

        public ComprobantesController(ComprobanteService comprobanteService)
        {
            _comprobanteService = comprobanteService;
        }

        private int UsuarioId => JwtHelper.GetUsuarioId(User);

        /// <summary>
        /// Importa desde archivos: el reporte .txt de "Comprobantes electrónicos recibidos" de SRI en Línea
        /// y/o los XML que llegan al correo. Cada clave encontrada se verifica en el SRI.
        /// </summary>
        [HttpPost("importar")]
        [RequestSizeLimit(MaxBytesArchivos)]
        public async Task<ActionResult<ImportacionResponse>> ImportarArchivos([FromForm] List<IFormFile> archivos, CancellationToken ct)
        {
            if (archivos.Count == 0)
                throw new BusinessException("Adjunta al menos un archivo .txt o .xml.");

            var contenido = new StringBuilder();
            foreach (var archivo in archivos)
            {
                // El reporte del SRI puede venir en Latin-1; las claves son ASCII, así que la codificación no afecta.
                using var lector = new StreamReader(archivo.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                contenido.AppendLine(await lector.ReadToEndAsync(ct));
            }

            return Ok(await _comprobanteService.ImportarAsync(UsuarioId, contenido.ToString(), ct));
        }

        /// <summary>Importa claves de acceso pegadas como texto (una o varias, en cualquier formato).</summary>
        [HttpPost("importar/claves")]
        public async Task<ActionResult<ImportacionResponse>> ImportarClaves(ImportarClavesRequest request, CancellationToken ct) =>
            Ok(await _comprobanteService.ImportarAsync(UsuarioId, request.Texto, ct));

        // GET api/comprobantes?anio=2026&mes=10&categoria=Salud&ruc=1790012345001&q=farmacia  (gastos: comprobantes recibidos)
        [HttpGet]
        public async Task<ActionResult<List<ComprobanteListaDto>>> GetComprobantes(
            [FromQuery, Range(2000, 2100)] int? anio,
            [FromQuery, Range(1, 12)] int? mes,
            [FromQuery] CategoriaGasto? categoria,
            [FromQuery] string? ruc,
            [FromQuery] string? q)
        {
            if (mes.HasValue && !anio.HasValue)
                throw new BusinessException("Para filtrar por mes indica también el año.");

            var filtro = new FiltroComprobantes(Direccion.Recibido, anio, mes, categoria, ruc, q);
            return Ok(await _comprobanteService.ObtenerComprobantesAsync(UsuarioId, filtro));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ComprobanteDto>> GetComprobante(int id) =>
            Ok(await _comprobanteService.ObtenerComprobanteAsync(UsuarioId, id));

        [HttpGet("{id:int}/xml")]
        public async Task<IActionResult> GetXml(int id)
        {
            var xml = await _comprobanteService.ObtenerXmlAsync(UsuarioId, id);
            return File(Encoding.UTF8.GetBytes(xml), "application/xml", $"comprobante-{id}.xml");
        }

        /// <summary>Cambia la categoría; por defecto la recuerda para el proveedor y la aplica a todos sus comprobantes.</summary>
        [HttpPut("{id:int}/categoria")]
        public async Task<ActionResult<ComprobanteDto>> CambiarCategoria(int id, CambiarCategoriaRequest request) =>
            Ok(await _comprobanteService.CambiarCategoriaAsync(UsuarioId, id, request));

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteComprobante(int id)
        {
            await _comprobanteService.EliminarComprobanteAsync(UsuarioId, id);
            return NoContent();
        }

        // GET api/comprobantes/resumen?anio=2026&mes=10  (sin mes: resumen anual con desglose por mes)
        [HttpGet("resumen")]
        public async Task<ActionResult<ResumenDto>> GetResumen(
            [FromQuery, Required, Range(2000, 2100)] int anio,
            [FromQuery, Range(1, 12)] int? mes) =>
            Ok(await _comprobanteService.ObtenerResumenAsync(UsuarioId, anio, mes));

        [HttpGet("proveedores")]
        public async Task<ActionResult<List<ProveedorDto>>> GetProveedores() =>
            Ok(await _comprobanteService.ObtenerProveedoresAsync(UsuarioId));

        [HttpPut("proveedores/{ruc}/categoria")]
        public async Task<IActionResult> AsignarCategoriaProveedor(
            [RegularExpression(@"^\d{13}$")] string ruc,
            [FromBody] CambiarCategoriaRequest request)
        {
            await _comprobanteService.AsignarCategoriaProveedorAsync(UsuarioId, ruc, request.Categoria);
            return NoContent();
        }
    }
}
