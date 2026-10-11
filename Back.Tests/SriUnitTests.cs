using Back.Models;
using Back.Sri;

namespace Back.Tests
{
    public class ClaveAccesoTests
    {
        [Fact]
        public void DigitoVerificador_EjemploDeLaFichaTecnicaDelSri()
        {
            // Ejemplo de la ficha técnica: 41261533 → suma ponderada 104 → 11 - (104 % 11) = 6
            Assert.Equal(6, ClaveAcceso.DigitoVerificador("41261533"));
        }

        [Fact]
        public void EsValida_RechazaClaveConUnDigitoAlterado()
        {
            var clave = SriFixtures.Clave(new DateOnly(2026, 10, 1), "01", SriFixtures.RucFarmacia, 123);
            Assert.True(ClaveAcceso.EsValida(clave));

            var alterada = clave[..20] + (clave[20] == '9' ? '0' : (char)(clave[20] + 1)) + clave[21..];
            Assert.False(ClaveAcceso.EsValida(alterada));
        }

        [Fact]
        public void Extraer_LeeReporteTxtDelSriYDescartaRuido()
        {
            var factura = SriFixtures.Clave(new DateOnly(2026, 10, 3), "01", SriFixtures.RucFarmacia, 1);
            var notaCredito = SriFixtures.Clave(new DateOnly(2026, 10, 4), "04", SriFixtures.RucFarmacia, 2);
            var invalida = factura[..48] + ((factura[48] - '0' + 1) % 10);

            // Formato tabulado del reporte de "Comprobantes electrónicos recibidos"; la clave aparece también como n.º de autorización.
            var reporte = $"""
                COMPROBANTE	SERIE_COMPROBANTE	RUC_EMISOR	RAZON_SOCIAL_EMISOR	FECHA_EMISION	CLAVE_ACCESO	NUMERO_AUTORIZACION	IMPORTE_TOTAL
                Factura	001-001-000000001	{SriFixtures.RucFarmacia}	FARMACIA	03/10/2026	{factura}	{factura}	33.00
                Notas de Crédito	001-001-000000002	{SriFixtures.RucFarmacia}	FARMACIA	04/10/2026	{notaCredito}	{notaCredito}	11.50
                Inválida	{invalida}	número largo: {factura}0
                """;

            Assert.Equal(new[] { factura, notaCredito }, ClaveAcceso.Extraer(reporte));
        }

        [Fact]
        public void Partes_DeLaClave()
        {
            var clave = SriFixtures.Clave(new DateOnly(2026, 10, 1), "04", SriFixtures.RucSupermercado, 9, ambiente: "1");
            Assert.Equal("04", ClaveAcceso.TipoComprobante(clave));
            Assert.Equal(SriFixtures.RucSupermercado, ClaveAcceso.RucEmisor(clave));
            Assert.Equal("1", ClaveAcceso.Ambiente(clave));
        }
    }

    public class ComprobanteXmlParserTests
    {
        private static readonly DateOnly Fecha = new(2026, 10, 3);

        [Fact]
        public void Factura_TotalesImpuestosYDetalle()
        {
            var clave = SriFixtures.Clave(Fecha, "01", SriFixtures.RucFarmacia, 77);
            var c = ComprobanteXmlParser.Parse(SriFixtures.FacturaXml(clave, SriFixtures.RucFarmacia, "FARMACIA DE PRUEBA S.A.", Fecha));

            Assert.Equal(TiposComprobante.Factura, c.TipoComprobante);
            Assert.Equal(clave, c.ClaveAcceso);
            Assert.Equal("001-001-000000077", c.NumeroComprobante);
            Assert.Equal(SriFixtures.RucFarmacia, c.RucEmisor);
            Assert.Equal("FARMACIA DE PRUEBA S.A. Comercial", c.NombreComercial);
            Assert.Equal(Fecha, c.FechaEmision);
            Assert.Equal(SriFixtures.MiCedula, c.IdentificacionComprador);
            Assert.Equal(30.00m, c.Subtotal);
            Assert.Equal(10.00m, c.BaseIva0);
            Assert.Equal(20.00m, c.BaseIvaGravada);
            Assert.Equal(3.00m, c.Iva);
            Assert.Equal(1.50m, c.Descuento);
            Assert.Equal(33.00m, c.Total);
            Assert.Equal(1, c.Signo);

            Assert.Equal(2, c.Detalles.Count);
            Assert.Equal("Vitamina C & Zinc", c.Detalles[1].Descripcion);
            Assert.Equal(21.5m, c.Detalles[1].PrecioUnitario);
            Assert.Equal(20.00m, c.Detalles[1].Subtotal);
        }

        [Fact]
        public void NotaCredito_UsaValorModificacionYRestaDelGasto()
        {
            var clave = SriFixtures.Clave(Fecha, "04", SriFixtures.RucFarmacia, 5);
            var c = ComprobanteXmlParser.Parse(SriFixtures.NotaCreditoXml(clave, SriFixtures.RucFarmacia, Fecha));

            Assert.Equal(TiposComprobante.NotaCredito, c.TipoComprobante);
            Assert.Equal(11.50m, c.Total);
            Assert.Equal(1.50m, c.Iva);
            Assert.Equal("001-001-000000001", c.DocumentoModificado);
            Assert.Equal("B2", Assert.Single(c.Detalles).Codigo);
            Assert.Equal(-1, c.Signo);
        }

        [Fact]
        public void TipoNoSoportado_Falla()
        {
            Assert.Throws<NotSupportedException>(() => ComprobanteXmlParser.Parse("<comprobanteRetencion/>"));
        }
    }

    public class SriRespuestaTests
    {
        private readonly string _clave = SriFixtures.Clave(new DateOnly(2026, 10, 3), "01", SriFixtures.RucFarmacia, 1);

        [Fact]
        public void Autorizado_ExtraeComprobanteYFechaEnUtc()
        {
            var xml = SriFixtures.FacturaXml(_clave, SriFixtures.RucFarmacia, "FARMACIA", new DateOnly(2026, 10, 3));
            var r = SriAutorizacionClient.Interpretar(SriFixtures.RespuestaAutorizada(_clave, xml));

            Assert.Equal(EstadoAutorizacion.Autorizado, r.Estado);
            Assert.Equal(_clave, r.NumeroAutorizacion);
            Assert.Equal(new DateTime(2026, 10, 5, 15, 22, 33, DateTimeKind.Utc), r.FechaAutorizacion);
            Assert.Equal(xml.Trim(), r.ComprobanteXml);
        }

        [Fact]
        public void NoAutorizado_IncluyeMensajesDelSri()
        {
            var r = SriAutorizacionClient.Interpretar(SriFixtures.RespuestaNoAutorizada(_clave));
            Assert.Equal(EstadoAutorizacion.NoAutorizado, r.Estado);
            Assert.Contains("FIRMA INVALIDA", r.Mensaje);
        }

        [Fact]
        public void SinAutorizaciones_EsNoEncontrado()
        {
            var r = SriAutorizacionClient.Interpretar(SriFixtures.RespuestaNoEncontrada(_clave));
            Assert.Equal(EstadoAutorizacion.NoEncontrado, r.Estado);
        }
    }

    public class IdentificacionTests
    {
        [Theory]
        [InlineData("0912345675")]
        [InlineData("1710034065")]
        public void CedulaValida(string cedula) => Assert.True(Identificacion.EsCedulaValida(cedula));

        [Theory]
        [InlineData("0912345678")] // dígito verificador incorrecto
        [InlineData("2512345675")] // provincia inexistente
        [InlineData("0962345675")] // tercer dígito de sociedad
        [InlineData("091234567")]
        [InlineData("09123456A5")]
        [InlineData(null)]
        public void CedulaInvalida(string? cedula) => Assert.False(Identificacion.EsCedulaValida(cedula));

        [Theory]
        [InlineData("0912345675001")] // persona natural
        [InlineData("1790010937001")] // sociedad privada
        [InlineData("1760001550001")] // entidad pública
        public void RucValido(string ruc) => Assert.True(Identificacion.EsRucValido(ruc));

        [Theory]
        [InlineData("0912345675000")] // establecimiento 000
        [InlineData("0912345678001")] // cédula base inválida
        [InlineData("1770010937001")] // tercer dígito 7 no existe
        [InlineData("1790010937")]
        public void RucInvalido(string ruc) => Assert.False(Identificacion.EsRucValido(ruc));
    }
}
