using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace Back.Sri
{
    public class SriOptions
    {
        public const string Section = "Sri";

        public string UrlProduccion { get; set; } = "https://cel.sri.gob.ec/comprobantes-electronicos-ws/AutorizacionComprobantesOffline";
        public string UrlPruebas { get; set; } = "https://celcer.sri.gob.ec/comprobantes-electronicos-ws/AutorizacionComprobantesOffline";
        public int TimeoutSegundos { get; set; } = 30;
        public int Reintentos { get; set; } = 3;
        public int ConsultasEnParalelo { get; set; } = 4;
    }

    public enum EstadoAutorizacion { Autorizado, NoAutorizado, NoEncontrado }

    public record SriAutorizacion(
        EstadoAutorizacion Estado,
        string? NumeroAutorizacion = null,
        DateTime? FechaAutorizacion = null,
        string? ComprobanteXml = null,
        string? Mensaje = null);

    /// <summary>Cliente del servicio web público AutorizacionComprobantesOffline del SRI (SOAP 1.1, sin credenciales).</summary>
    public class SriAutorizacionClient
    {
        private readonly HttpClient _http;
        private readonly SriOptions _options;

        public SriAutorizacionClient(HttpClient http, IOptions<SriOptions> options)
        {
            _http = http;
            _options = options.Value;
        }

        public async Task<SriAutorizacion> ConsultarAsync(string claveAcceso, CancellationToken ct = default)
        {
            // El dígito de ambiente de la clave indica a qué servidor pertenece el comprobante.
            var url = ClaveAcceso.Ambiente(claveAcceso) == "1" ? _options.UrlPruebas : _options.UrlProduccion;
            var envelope =
                "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:ec=\"http://ec.gob.sri.ws.autorizacion\">" +
                "<soapenv:Header/><soapenv:Body><ec:autorizacionComprobante>" +
                $"<claveAccesoComprobante>{claveAcceso}</claveAccesoComprobante>" +
                "</ec:autorizacionComprobante></soapenv:Body></soapenv:Envelope>";

            for (var intento = 1; ; intento++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(envelope, Encoding.UTF8, "text/xml"),
                    };
                    request.Headers.Add("SOAPAction", "\"\"");

                    using var response = await _http.SendAsync(request, ct);
                    response.EnsureSuccessStatusCode();
                    return Interpretar(await response.Content.ReadAsStringAsync(ct));
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                           && intento < _options.Reintentos && !ct.IsCancellationRequested)
                {
                    // El servicio del SRI es intermitente: reintenta con espera creciente.
                    await Task.Delay(TimeSpan.FromSeconds(intento), ct);
                }
            }
        }

        public static SriAutorizacion Interpretar(string respuestaSoap)
        {
            var autorizaciones = XDocument.Parse(respuestaSoap).Descendants()
                .Where(e => e.Name.LocalName == "autorizacion")
                .ToList();
            if (autorizaciones.Count == 0)
                return new SriAutorizacion(EstadoAutorizacion.NoEncontrado, Mensaje: "El SRI no tiene registrado este comprobante.");

            // El SRI puede devolver el historial de intentos; basta con uno autorizado.
            var autorizada = autorizaciones.FirstOrDefault(a => Texto(a, "estado") == "AUTORIZADO");
            if (autorizada is null)
            {
                var mensajes = autorizaciones[0].Descendants()
                    .Where(e => e.Name.LocalName == "mensaje" && e.HasElements)
                    .Select(m => $"{Texto(m, "identificador")}: {Texto(m, "mensaje")} {Texto(m, "informacionAdicional")}".Trim());
                return new SriAutorizacion(EstadoAutorizacion.NoAutorizado, Mensaje: string.Join(" | ", mensajes));
            }

            return new SriAutorizacion(
                EstadoAutorizacion.Autorizado,
                Texto(autorizada, "numeroAutorizacion"),
                ParsearFecha(Texto(autorizada, "fechaAutorizacion")),
                Texto(autorizada, "comprobante"));
        }

        private static string? Texto(XElement padre, string nombre) =>
            padre.Elements().FirstOrDefault(e => e.Name.LocalName == nombre)?.Value.Trim();

        // Formatos vistos: "2024-03-15T10:22:33-05:00" (actual) y "15/03/2024 10:22:33" (antiguo, hora de Ecuador).
        private static DateTime? ParsearFecha(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return null;
            if (DateTimeOffset.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out var conZona) && valor.Contains('T'))
                return conZona.UtcDateTime;
            if (DateTime.TryParseExact(valor, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
                return DateTime.SpecifyKind(local.AddHours(5), DateTimeKind.Utc);
            return null;
        }
    }
}
