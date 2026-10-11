using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Back.Dtos;
using Back.Models;
using Back.Sri;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Back.Tests
{
    /// <summary>Simula el servicio web del SRI: responde según la clave consultada y registra las llamadas.</summary>
    public class FakeSriHandler : HttpMessageHandler
    {
        public Dictionary<string, string> Respuestas { get; } = new();
        public HashSet<string> ClavesQueFallan { get; } = new();
        public List<(string Url, string Clave, string? SoapAction)> Llamadas { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var cuerpo = await request.Content!.ReadAsStringAsync(ct);
            var clave = Regex.Match(cuerpo, "<claveAccesoComprobante>(\\d+)</claveAccesoComprobante>").Groups[1].Value;
            lock (Llamadas)
                Llamadas.Add((request.RequestUri!.ToString(), clave, request.Headers.TryGetValues("SOAPAction", out var v) ? v.First() : null));

            if (ClavesQueFallan.Contains(clave))
                throw new HttpRequestException("Conexión rechazada (simulada)");

            var xml = Respuestas.TryGetValue(clave, out var r) ? r : SriFixtures.RespuestaNoEncontrada(clave);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml, Encoding.UTF8, "text/xml") };
        }

        // IHttpClientFactory recicla handlers; este es compartido por toda la prueba.
        protected override void Dispose(bool disposing) { }
    }

    public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        // Servidor de pruebas: el PostgreSQL de docker-compose, o el que indique NUDO_TEST_DB.
        private static readonly string Servidor = Environment.GetEnvironmentVariable("NUDO_TEST_DB")
            ?? "Host=localhost;Port=5432;Username=nudo;Password=nudo_dev";
        private const string BaseDatos = "nudo_test";

        public FakeSriHandler Sri { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Default", $"{Servidor};Database={BaseDatos}");
            builder.UseSetting("Jwt:Key", new string('k', 48));
            builder.UseSetting("Database:MigrateOnStartup", "true");
            builder.UseSetting("Sri:Reintentos", "2");
            builder.ConfigureServices(services =>
                services.AddHttpClient<SriAutorizacionClient>().ConfigurePrimaryHttpMessageHandler(() => Sri));
        }

        public async Task InitializeAsync()
        {
            // Base limpia en cada corrida; la API la recrea con las migraciones al arrancar.
            await using var conexion = new NpgsqlConnection($"{Servidor};Database=postgres");
            await conexion.OpenAsync();
            await using var comando = new NpgsqlCommand($"DROP DATABASE IF EXISTS {BaseDatos} WITH (FORCE)", conexion);
            await comando.ExecuteNonQueryAsync();
        }

        Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
    }

    public partial class ImportacionSriTests : IClassFixture<ApiFactory>
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

        private readonly ApiFactory _factory;

        public ImportacionSriTests(ApiFactory factory)
        {
            _factory = factory;
        }

        private async Task<HttpClient> ClienteAutenticadoAsync(string correo)
        {
            var http = _factory.CreateClient();
            var registro = await http.PostAsJsonAsync("/api/auth/register", new { username = "Usuario", email = correo, password = "Secreta123" });
            registro.EnsureSuccessStatusCode();
            var auth = await registro.Content.ReadFromJsonAsync<AuthResponse>(Json);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
            return http;
        }

        private string Autorizar(string clave, string xml)
        {
            _factory.Sri.Respuestas[clave] = SriFixtures.RespuestaAutorizada(clave, xml);
            return clave;
        }

        private static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta)
        {
            Assert.True(respuesta.IsSuccessStatusCode, $"{(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
            return (await respuesta.Content.ReadFromJsonAsync<T>(Json))!;
        }

        [Fact]
        public async Task FlujoCompleto_ImportarCategorizarYResumir()
        {
            var http = await ClienteAutenticadoAsync("yo@test.ec");
            var oct = (int dia) => new DateOnly(2026, 10, dia);

            var facturaFarmacia = Autorizar(
                SriFixtures.Clave(oct(3), "01", SriFixtures.RucFarmacia, 1),
                SriFixtures.FacturaXml(SriFixtures.Clave(oct(3), "01", SriFixtures.RucFarmacia, 1), SriFixtures.RucFarmacia, "FARMACIA DE PRUEBA S.A.", oct(3)));
            var notaCredito = Autorizar(
                SriFixtures.Clave(oct(4), "04", SriFixtures.RucFarmacia, 2),
                SriFixtures.NotaCreditoXml(SriFixtures.Clave(oct(4), "04", SriFixtures.RucFarmacia, 2), SriFixtures.RucFarmacia, oct(4)));
            var claveSuper = SriFixtures.Clave(oct(10), "01", SriFixtures.RucSupermercado, 3);
            var xmlSuper = SriFixtures.FacturaXml(claveSuper, SriFixtures.RucSupermercado, "SUPERMERCADO DE PRUEBA", oct(10));
            Autorizar(claveSuper, xmlSuper);
            var septiembre = new DateOnly(2026, 9, 20);
            var facturaSeptiembre = Autorizar(
                SriFixtures.Clave(septiembre, "01", SriFixtures.RucFarmacia, 4),
                SriFixtures.FacturaXml(SriFixtures.Clave(septiembre, "01", SriFixtures.RucFarmacia, 4), SriFixtures.RucFarmacia, "FARMACIA DE PRUEBA S.A.", septiembre));

            var noAutorizada = SriFixtures.Clave(oct(5), "01", SriFixtures.RucFarmacia, 5);
            _factory.Sri.Respuestas[noAutorizada] = SriFixtures.RespuestaNoAutorizada(noAutorizada);
            var noEncontradaPruebas = SriFixtures.Clave(oct(6), "01", SriFixtures.RucFarmacia, 6, ambiente: "1");
            var retencion = SriFixtures.Clave(oct(7), "07", SriFixtures.RucFarmacia, 7);
            var caida = SriFixtures.Clave(oct(8), "01", SriFixtures.RucFarmacia, 8);
            _factory.Sri.ClavesQueFallan.Add(caida);

            // Reporte .txt del SRI + XML del supermercado recibido por correo (envuelto en <autorizacion>).
            var reporte = string.Join("\n", new[] { facturaFarmacia, notaCredito, facturaSeptiembre, noAutorizada, noEncontradaPruebas, retencion, caida }
                .Select((c, i) => $"Factura\t001-001-{i:D9}\t{c[10..23]}\tEMISOR\t{c[..2]}/{c[2..4]}/{c[4..8]}\t{c}\t{c}\t10.00"));
            var correo = $"<autorizacion><estado>AUTORIZADO</estado><numeroAutorizacion>{claveSuper}</numeroAutorizacion><comprobante><![CDATA[{xmlSuper}]]></comprobante></autorizacion>";

            using var formulario = new MultipartFormDataContent
            {
                { new ByteArrayContent(Encoding.Latin1.GetBytes(reporte)), "archivos", "reporte.txt" },
                { new StringContent(correo), "archivos", "factura-super.xml" },
            };
            var importacion = await LeerAsync<ImportacionResponse>(await http.PostAsync("/api/comprobantes/importar", formulario));

            Assert.Equal(8, importacion.ClavesEncontradas);
            Assert.Equal(4, importacion.Importados);
            Assert.Equal(4, importacion.ConProblemas);
            var resultado = importacion.Claves.ToDictionary(c => c.ClaveAcceso, c => c.Resultado);
            Assert.Equal(ResultadoImportacion.NoAutorizado, resultado[noAutorizada]);
            Assert.Equal(ResultadoImportacion.NoEncontrado, resultado[noEncontradaPruebas]);
            Assert.Equal(ResultadoImportacion.NoSoportado, resultado[retencion]);
            Assert.Equal(ResultadoImportacion.Error, resultado[caida]);

            // Ruteo por ambiente, SOAPAction, sin consultar retenciones y con reintento ante caídas.
            Assert.All(_factory.Sri.Llamadas, l => Assert.Equal("\"\"", l.SoapAction));
            Assert.Contains("celcer.sri.gob.ec", _factory.Sri.Llamadas.Single(l => l.Clave == noEncontradaPruebas).Url);
            Assert.Contains("://cel.sri.gob.ec", _factory.Sri.Llamadas.First(l => l.Clave == facturaFarmacia).Url);
            Assert.DoesNotContain(_factory.Sri.Llamadas, l => l.Clave == retencion);
            Assert.Equal(2, _factory.Sri.Llamadas.Count(l => l.Clave == caida));

            // Reimportar no duplica ni vuelve a consultar lo que ya está guardado.
            var llamadasPrevias = _factory.Sri.Llamadas.Count(l => l.Clave == facturaFarmacia);
            var reimportacion = await LeerAsync<ImportacionResponse>(await http.PostAsJsonAsync("/api/comprobantes/importar/claves", new { texto = $"{facturaFarmacia} {claveSuper}" }));
            Assert.Equal(2, reimportacion.YaExistian);
            Assert.Equal(llamadasPrevias, _factory.Sri.Llamadas.Count(l => l.Clave == facturaFarmacia));

            // Listado de octubre.
            var octubre = await LeerAsync<List<ComprobanteListaDto>>(await http.GetAsync("/api/comprobantes?anio=2026&mes=10"));
            Assert.Equal(3, octubre.Count);
            Assert.All(octubre, c => Assert.Equal(CategoriaGasto.SinCategoria, c.Categoria));

            // Categorizar la farmacia como Salud se aplica a todos sus comprobantes, incluida la nota de crédito.
            var idFarmacia = octubre.Single(c => c.ClaveAcceso == facturaFarmacia).Id;
            var actualizado = await LeerAsync<ComprobanteDto>(await http.PutAsJsonAsync($"/api/comprobantes/{idFarmacia}/categoria", new { categoria = "Salud" }));
            Assert.Equal(CategoriaGasto.Salud, actualizado.Categoria);
            Assert.Equal(2, actualizado.Detalles.Count);
            var deLaFarmacia = await LeerAsync<List<ComprobanteListaDto>>(await http.GetAsync($"/api/comprobantes?ruc={SriFixtures.RucFarmacia}"));
            Assert.Equal(3, deLaFarmacia.Count);
            Assert.All(deLaFarmacia, c => Assert.Equal(CategoriaGasto.Salud, c.Categoria));

            // Una factura nueva del mismo proveedor entra ya categorizada.
            var nueva = Autorizar(
                SriFixtures.Clave(oct(15), "01", SriFixtures.RucFarmacia, 9),
                SriFixtures.FacturaXml(SriFixtures.Clave(oct(15), "01", SriFixtures.RucFarmacia, 9), SriFixtures.RucFarmacia, "FARMACIA DE PRUEBA S.A.", oct(15)));
            await LeerAsync<ImportacionResponse>(await http.PostAsJsonAsync("/api/comprobantes/importar/claves", new { texto = nueva }));
            var nuevaGuardada = (await LeerAsync<List<ComprobanteListaDto>>(await http.GetAsync("/api/comprobantes?anio=2026&mes=10&categoria=Salud")))
                .Single(c => c.ClaveAcceso == nueva);
            Assert.Equal(33.00m, nuevaGuardada.Total);

            // Resumen de octubre: 33 + 33 + 33 - 11.50 (nota de crédito).
            var resumen = await LeerAsync<ResumenDto>(await http.GetAsync("/api/comprobantes/resumen?anio=2026&mes=10"));
            Assert.Equal(4, resumen.Comprobantes);
            Assert.Equal(87.50m, resumen.Total);
            Assert.Equal(54.50m, resumen.TotalDeducible);
            Assert.Equal(1, resumen.SinCategoria);
            Assert.Equal(54.50m, resumen.PorCategoria.Single(c => c.Categoria == CategoriaGasto.Salud).Total);
            Assert.Null(resumen.PorMes);

            var anual = await LeerAsync<ResumenDto>(await http.GetAsync("/api/comprobantes/resumen?anio=2026"));
            Assert.Equal(33.00m, anual.PorMes![8].Total);
            Assert.Equal(87.50m, anual.PorMes[9].Total);

            // XML original descargable.
            var xml = await http.GetStringAsync($"/api/comprobantes/{idFarmacia}/xml");
            Assert.Contains(facturaFarmacia, xml);

            // Otro usuario no ve mis comprobantes.
            var otro = await ClienteAutenticadoAsync("otro@test.ec");
            Assert.Equal(HttpStatusCode.NotFound, (await otro.GetAsync($"/api/comprobantes/{idFarmacia}")).StatusCode);
            Assert.Empty(await LeerAsync<List<ComprobanteListaDto>>(await otro.GetAsync("/api/comprobantes")));
        }

        [Fact]
        public async Task Importar_SinClavesValidas_Devuelve400()
        {
            var http = await ClienteAutenticadoAsync("vacio@test.ec");
            var respuesta = await http.PostAsJsonAsync("/api/comprobantes/importar/claves", new { texto = "no hay claves aquí 12345" });
            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }

        [Fact]
        public async Task SinToken_Devuelve401()
        {
            var respuesta = await _factory.CreateClient().GetAsync("/api/comprobantes");
            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        }

        [Fact]
        public async Task Perfil_ValidaIdentificacionYCompletaCedulaDesdeRuc()
        {
            var http = await ClienteAutenticadoAsync("perfil@test.ec");

            var inicial = await LeerAsync<PerfilDto>(await http.GetAsync("/api/perfil"));
            Assert.False(inicial.Completo);

            var cedulaMala = await http.PutAsJsonAsync("/api/perfil", new { cedula = "0912345678", cargasFamiliares = 0 });
            Assert.Equal(HttpStatusCode.BadRequest, cedulaMala.StatusCode);

            var rucAjeno = await http.PutAsJsonAsync("/api/perfil", new { cedula = "1710034065", ruc = SriFixtures.MiRuc, cargasFamiliares = 0 });
            Assert.Equal(HttpStatusCode.BadRequest, rucAjeno.StatusCode);

            var guardado = await LeerAsync<PerfilActualizadoDto>(await http.PutAsJsonAsync("/api/perfil",
                new { ruc = "0912345675-001", razonSocial = "David Alvarado", regimen = "General", cargasFamiliares = 2, enfermedadCatastrofica = false }));
            Assert.Equal(SriFixtures.MiCedula, guardado.Perfil.Cedula);
            Assert.Equal(SriFixtures.MiRuc, guardado.Perfil.Ruc);
            Assert.Equal("DAVID ALVARADO", guardado.Perfil.RazonSocial);
            Assert.Equal(2, guardado.Perfil.CargasFamiliares);
            Assert.True(guardado.Perfil.Completo);
        }

        [Fact]
        public async Task Perfil_ExcluyeLoQueEmitoDeLosGastosYMarcaFacturasAjenas()
        {
            var http = await ClienteAutenticadoAsync("ingresos@test.ec");
            var fecha = new DateOnly(2026, 10, 12);

            // Factura que yo emití a un cliente, importada ANTES de registrar mi RUC: entra como gasto.
            var emitida = Autorizar(
                SriFixtures.Clave(fecha, "01", SriFixtures.MiRuc, 501),
                SriFixtures.FacturaXml(SriFixtures.Clave(fecha, "01", SriFixtures.MiRuc, 501), SriFixtures.MiRuc, "DAVID ALVARADO", fecha,
                    comprador: "1790010937001", razonComprador: "CLIENTE EJEMPLO S.A."));
            var primera = await LeerAsync<ImportacionResponse>(await http.PostAsJsonAsync("/api/comprobantes/importar/claves", new { texto = emitida }));
            Assert.False(primera.PerfilCompleto);
            Assert.Equal(0, primera.Ingresos);

            // Al registrar el RUC se reclasifica como ingreso.
            var perfil = await LeerAsync<PerfilActualizadoDto>(await http.PutAsJsonAsync("/api/perfil",
                new { ruc = SriFixtures.MiRuc, regimen = "General", cargasFamiliares = 0 }));
            Assert.Equal(1, perfil.ComprobantesEmitidos);

            // Desde ahora las nuevas se clasifican al importar: otra emitida, un gasto mío y uno a nombre de otra persona.
            var emitida2 = Autorizar(
                SriFixtures.Clave(fecha, "01", SriFixtures.MiRuc, 502),
                SriFixtures.FacturaXml(SriFixtures.Clave(fecha, "01", SriFixtures.MiRuc, 502), SriFixtures.MiRuc, "DAVID ALVARADO", fecha,
                    comprador: "1790010937001", razonComprador: "CLIENTE EJEMPLO S.A."));
            var gastoMio = Autorizar(
                SriFixtures.Clave(fecha, "01", SriFixtures.RucSupermercado, 503),
                SriFixtures.FacturaXml(SriFixtures.Clave(fecha, "01", SriFixtures.RucSupermercado, 503), SriFixtures.RucSupermercado, "SUPERMERCADO", fecha,
                    comprador: SriFixtures.MiRuc));
            var gastoAjeno = Autorizar(
                SriFixtures.Clave(fecha, "01", SriFixtures.RucFarmacia, 504),
                SriFixtures.FacturaXml(SriFixtures.Clave(fecha, "01", SriFixtures.RucFarmacia, 504), SriFixtures.RucFarmacia, "FARMACIA", fecha,
                    comprador: "1710034065", razonComprador: "MARIA ALVARADO"));
            var segunda = await LeerAsync<ImportacionResponse>(await http.PostAsJsonAsync("/api/comprobantes/importar/claves",
                new { texto = $"{emitida2} {gastoMio} {gastoAjeno}" }));
            Assert.Equal(3, segunda.Importados);
            Assert.Equal(1, segunda.Ingresos);
            Assert.True(segunda.PerfilCompleto);

            var gastos = await LeerAsync<List<ComprobanteListaDto>>(await http.GetAsync("/api/comprobantes?anio=2026&mes=10"));
            Assert.Equal(2, gastos.Count);
            Assert.True(gastos.Single(g => g.ClaveAcceso == gastoMio).ANombreDelUsuario);
            Assert.False(gastos.Single(g => g.ClaveAcceso == gastoAjeno).ANombreDelUsuario);

            var resumen = await LeerAsync<ResumenDto>(await http.GetAsync("/api/comprobantes/resumen?anio=2026&mes=10"));
            Assert.Equal(2, resumen.Comprobantes);
            Assert.Equal(66.00m, resumen.Total);
            Assert.Equal(1, resumen.ANombreDeOtros);

            // Si quito el RUC del perfil, lo que emití vuelve a contarse como gasto.
            await LeerAsync<PerfilActualizadoDto>(await http.PutAsJsonAsync("/api/perfil", new { cargasFamiliares = 0 }));
            Assert.Equal(4, (await LeerAsync<List<ComprobanteListaDto>>(await http.GetAsync("/api/comprobantes?anio=2026&mes=10"))).Count);
        }
    }
}
