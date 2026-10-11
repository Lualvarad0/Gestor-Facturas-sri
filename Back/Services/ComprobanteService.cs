using System.Collections.Concurrent;
using System.Xml;
using Back.Dtos;
using Back.Helpers;
using Back.Interfaces;
using Back.Models;
using Back.Sri;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Back.Services
{
    public class ComprobanteService
    {
        public const int MaxClavesPorImportacion = 500;

        private static readonly HashSet<CategoriaGasto> Deducibles = new()
        {
            CategoriaGasto.Vivienda, CategoriaGasto.Educacion, CategoriaGasto.Salud,
            CategoriaGasto.Alimentacion, CategoriaGasto.Vestimenta, CategoriaGasto.Turismo,
        };

        private readonly IComprobanteRepository _repository;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly SriAutorizacionClient _sri;
        private readonly SriOptions _sriOptions;
        private readonly ILogger<ComprobanteService> _logger;

        public ComprobanteService(
            IComprobanteRepository repository,
            IUsuarioRepository usuarioRepository,
            SriAutorizacionClient sri,
            IOptions<SriOptions> sriOptions,
            ILogger<ComprobanteService> logger)
        {
            _repository = repository;
            _usuarioRepository = usuarioRepository;
            _sri = sri;
            _sriOptions = sriOptions.Value;
            _logger = logger;
        }

        /// <summary>
        /// Busca claves de acceso en el texto (reporte .txt del SRI, XML de correo o claves pegadas),
        /// consulta cada clave nueva en el SRI y guarda los comprobantes autorizados.
        /// Los emitidos con el RUC del perfil quedan como ingresos; el resto, como gastos.
        /// </summary>
        public async Task<ImportacionResponse> ImportarAsync(int usuarioId, string texto, CancellationToken ct)
        {
            var claves = ClaveAcceso.Extraer(texto);
            if (claves.Count == 0)
                throw new BusinessException("No se encontraron claves de acceso válidas (49 dígitos) en el contenido enviado.");
            if (claves.Count > MaxClavesPorImportacion)
                throw new BusinessException($"Se encontraron {claves.Count} claves; el máximo por importación es {MaxClavesPorImportacion}.");

            var perfil = await ObtenerPerfilAsync(usuarioId);
            var resultados = new ConcurrentDictionary<string, ClaveImportada>();
            var existentes = await _repository.GetClavesExistentesAsync(usuarioId, claves);
            var porConsultar = new List<string>();
            foreach (var clave in claves)
            {
                var tipo = ClaveAcceso.TipoComprobante(clave);
                if (existentes.Contains(clave))
                    resultados[clave] = new(clave, ResultadoImportacion.YaExistia);
                else if (!TiposComprobante.Soportados.Contains(tipo))
                    resultados[clave] = new(clave, ResultadoImportacion.NoSoportado, $"{TiposComprobante.Nombre(tipo)}: todavía no se procesa.");
                else
                    porConsultar.Add(clave);
            }

            var nuevos = new ConcurrentBag<Comprobante>();
            var opciones = new ParallelOptions { MaxDegreeOfParallelism = _sriOptions.ConsultasEnParalelo, CancellationToken = ct };
            await Parallel.ForEachAsync(porConsultar, opciones, async (clave, token) =>
            {
                var (resultado, comprobante) = await ConsultarYParsearAsync(clave, token);
                if (comprobante is not null)
                {
                    comprobante.Direccion = comprobante.RucEmisor == perfil.Ruc ? Direccion.Emitido : Direccion.Recibido;
                    resultado = resultado with { Direccion = comprobante.Direccion };
                    nuevos.Add(comprobante);
                }
                resultados[clave] = resultado;
            });

            if (!nuevos.IsEmpty)
            {
                var categorias = await _repository.GetCategoriasProveedorAsync(usuarioId, nuevos.Select(c => c.RucEmisor).Distinct());
                foreach (var comprobante in nuevos)
                {
                    comprobante.UsuarioId = usuarioId;
                    comprobante.Categoria = comprobante.Direccion == Direccion.Recibido
                        ? categorias.GetValueOrDefault(comprobante.RucEmisor, CategoriaGasto.SinCategoria)
                        : CategoriaGasto.SinCategoria;
                }

                try
                {
                    await _repository.AddRangeAsync(nuevos);
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                {
                    throw BusinessException.Conflict("Otra importación guardó algunos de estos comprobantes al mismo tiempo. Vuelve a importar el archivo.");
                }
            }

            // Respeta el orden en que aparecieron las claves en el archivo.
            var lista = claves.Select(c => resultados[c]).ToList();
            return new ImportacionResponse(
                claves.Count,
                lista.Count(r => r.Resultado == ResultadoImportacion.Importado),
                lista.Count(r => r.Resultado == ResultadoImportacion.YaExistia),
                lista.Count(r => r.Resultado is not (ResultadoImportacion.Importado or ResultadoImportacion.YaExistia)),
                lista.Count(r => r.Resultado == ResultadoImportacion.Importado && r.Direccion == Direccion.Emitido),
                PerfilCompleto: !string.IsNullOrEmpty(perfil.Ruc),
                lista);
        }

        private async Task<(ClaveImportada, Comprobante?)> ConsultarYParsearAsync(string clave, CancellationToken ct)
        {
            SriAutorizacion autorizacion;
            try
            {
                autorizacion = await _sri.ConsultarAsync(clave, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or XmlException && !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "El SRI no respondió para la clave {Clave}", clave);
                return (new(clave, ResultadoImportacion.Error, "El SRI no respondió. Intenta de nuevo más tarde."), null);
            }

            if (autorizacion.Estado == EstadoAutorizacion.NoEncontrado)
                return (new(clave, ResultadoImportacion.NoEncontrado, autorizacion.Mensaje), null);
            if (autorizacion.Estado == EstadoAutorizacion.NoAutorizado || string.IsNullOrWhiteSpace(autorizacion.ComprobanteXml))
                return (new(clave, ResultadoImportacion.NoAutorizado, autorizacion.Mensaje), null);

            try
            {
                var comprobante = ComprobanteXmlParser.Parse(autorizacion.ComprobanteXml);
                if (comprobante.ClaveAcceso != clave)
                    return (new(clave, ResultadoImportacion.Error, "El XML devuelto por el SRI no corresponde a la clave consultada."), null);

                comprobante.NumeroAutorizacion = autorizacion.NumeroAutorizacion ?? clave;
                comprobante.FechaAutorizacion = autorizacion.FechaAutorizacion;
                comprobante.Xml = new ComprobanteXml { Contenido = autorizacion.ComprobanteXml };
                return (new(clave, ResultadoImportacion.Importado, $"{comprobante.RazonSocialEmisor} — ${comprobante.Total:0.00}"), comprobante);
            }
            catch (Exception ex) when (ex is XmlException or FormatException or NotSupportedException)
            {
                _logger.LogWarning(ex, "No se pudo leer el XML del comprobante {Clave}", clave);
                return (new(clave, ResultadoImportacion.Error, $"No se pudo leer el XML: {ex.Message}"), null);
            }
        }

        public async Task<List<ComprobanteListaDto>> ObtenerComprobantesAsync(int usuarioId, FiltroComprobantes filtro)
        {
            var perfil = await ObtenerPerfilAsync(usuarioId);
            return (await _repository.GetAllAsync(usuarioId, filtro)).Select(c => ComprobanteListaDto.From(c, perfil)).ToList();
        }

        public async Task<ComprobanteDto> ObtenerComprobanteAsync(int usuarioId, int id) =>
            ComprobanteDto.From(await ObtenerEntidadAsync(usuarioId, id, conDetalles: true), await ObtenerPerfilAsync(usuarioId));

        public async Task<string> ObtenerXmlAsync(int usuarioId, int id) =>
            await _repository.GetXmlAsync(usuarioId, id) ?? throw BusinessException.NotFound("Comprobante no encontrado.");

        public async Task<ComprobanteDto> CambiarCategoriaAsync(int usuarioId, int id, CambiarCategoriaRequest request)
        {
            var comprobante = await ObtenerEntidadAsync(usuarioId, id);
            if (comprobante.Direccion == Direccion.Emitido)
                throw new BusinessException("Las facturas que emites son ingresos y no llevan categoría de gasto.");

            // Se asigna también en la entidad cargada: ExecuteUpdate no actualiza las instancias que EF ya tiene en memoria.
            comprobante.Categoria = request.Categoria;
            if (request.AplicarAlProveedor)
                await _repository.AsignarCategoriaProveedorAsync(usuarioId, comprobante.RucEmisor, request.Categoria);
            else
                await _repository.SaveChangesAsync();

            return await ObtenerComprobanteAsync(usuarioId, id);
        }

        public async Task EliminarComprobanteAsync(int usuarioId, int id) =>
            await _repository.DeleteAsync(await ObtenerEntidadAsync(usuarioId, id));

        public async Task<List<ProveedorDto>> ObtenerProveedoresAsync(int usuarioId)
        {
            var comprobantes = await _repository.GetAllAsync(usuarioId, new FiltroComprobantes(Direccion.Recibido));
            var categorias = await _repository.GetCategoriasProveedorAsync(usuarioId);
            return comprobantes
                .GroupBy(c => c.RucEmisor)
                .Select(g => new ProveedorDto(
                    g.Key,
                    g.First().RazonSocialEmisor,
                    categorias.TryGetValue(g.Key, out var categoria) ? categoria : null,
                    g.Count(),
                    g.Sum(c => c.Signo * c.Total)))
                .OrderByDescending(p => p.Total)
                .ToList();
        }

        public async Task AsignarCategoriaProveedorAsync(int usuarioId, string ruc, CategoriaGasto categoria) =>
            await _repository.AsignarCategoriaProveedorAsync(usuarioId, ruc, categoria);

        public async Task<ResumenDto> ObtenerResumenAsync(int usuarioId, int anio, int? mes)
        {
            var perfil = await ObtenerPerfilAsync(usuarioId);
            var gastos = await _repository.GetAllAsync(usuarioId, new FiltroComprobantes(Direccion.Recibido, anio, mes));

            var porCategoria = gastos
                .GroupBy(c => c.Categoria)
                .Select(g => new TotalCategoriaDto(g.Key, g.Count(), g.Sum(c => c.Signo * c.Total)))
                .OrderByDescending(t => t.Total)
                .ToList();

            var proveedores = gastos
                .GroupBy(c => c.RucEmisor)
                .Select(g => new TotalTerceroDto(g.Key, g.First().RazonSocialEmisor, g.Count(), g.Sum(c => c.Signo * c.Total)))
                .OrderByDescending(t => t.Total)
                .Take(10)
                .ToList();

            var porMes = mes.HasValue ? null : Enumerable.Range(1, 12)
                .Select(m =>
                {
                    var gastosMes = gastos.Where(c => c.FechaEmision.Month == m).ToList();
                    return new TotalMesDto(
                        m,
                        gastosMes.Count,
                        gastosMes.Sum(c => c.Signo * c.Total),
                        gastosMes.Sum(c => c.Signo * c.Iva));
                })
                .ToList();

            return new ResumenDto(
                anio,
                mes,
                gastos.Count,
                gastos.Sum(c => c.Signo * c.Total),
                gastos.Sum(c => c.Signo * c.Iva),
                gastos.Where(c => Deducibles.Contains(c.Categoria)).Sum(c => c.Signo * c.Total),
                gastos.Count(c => c.Categoria == CategoriaGasto.SinCategoria),
                gastos.Count(c => ComprobanteListaDto.ANombreDe(c, perfil) == false),
                porCategoria,
                proveedores,
                porMes);
        }

        private async Task<Comprobante> ObtenerEntidadAsync(int usuarioId, int id, bool conDetalles = false) =>
            await _repository.GetByIdAsync(usuarioId, id, conDetalles) ?? throw BusinessException.NotFound("Comprobante no encontrado.");

        private async Task<Usuario> ObtenerPerfilAsync(int usuarioId) =>
            await _usuarioRepository.GetByIdAsync(usuarioId) ?? throw BusinessException.NotFound("Usuario no encontrado.");
    }
}
