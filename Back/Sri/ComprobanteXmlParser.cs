using System.Globalization;
using System.Xml.Linq;
using Back.Models;

namespace Back.Sri
{
    /// <summary>
    /// Lee el XML de un comprobante electrónico del SRI (factura, nota de crédito, nota de débito)
    /// y arma la entidad con sus totales y detalle. No asigna usuario ni categoría.
    /// </summary>
    public static class ComprobanteXmlParser
    {
        public static Comprobante Parse(string xml)
        {
            var raiz = XDocument.Parse(xml).Root ?? throw new FormatException("XML vacío.");
            var (tipo, nodoInfo) = raiz.Name.LocalName switch
            {
                "factura" => (TiposComprobante.Factura, "infoFactura"),
                "notaCredito" => (TiposComprobante.NotaCredito, "infoNotaCredito"),
                "notaDebito" => (TiposComprobante.NotaDebito, "infoNotaDebito"),
                var otro => throw new NotSupportedException($"Tipo de comprobante '{otro}' no soportado."),
            };

            var tributaria = Hijo(raiz, "infoTributaria") ?? throw new FormatException("Falta infoTributaria.");
            var info = Hijo(raiz, nodoInfo) ?? throw new FormatException($"Falta {nodoInfo}.");

            var comprobante = new Comprobante
            {
                ClaveAcceso = Texto(tributaria, "claveAcceso"),
                TipoComprobante = tipo,
                NumeroComprobante = $"{Texto(tributaria, "estab")}-{Texto(tributaria, "ptoEmi")}-{Texto(tributaria, "secuencial")}",
                RucEmisor = Texto(tributaria, "ruc"),
                RazonSocialEmisor = Texto(tributaria, "razonSocial"),
                NombreComercial = TextoOpcional(tributaria, "nombreComercial"),
                FechaEmision = DateOnly.ParseExact(Texto(info, "fechaEmision"), "dd/MM/yyyy", CultureInfo.InvariantCulture),
                IdentificacionComprador = Texto(info, "identificacionComprador"),
                RazonSocialComprador = Texto(info, "razonSocialComprador"),
                Subtotal = Decimal(info, "totalSinImpuestos"),
                Descuento = Decimal(info, "totalDescuento"),
                Propina = Decimal(info, "propina"),
                Total = tipo switch
                {
                    TiposComprobante.Factura => Decimal(info, "importeTotal"),
                    TiposComprobante.NotaCredito => Decimal(info, "valorModificacion"),
                    _ => Decimal(info, "valorTotal"),
                },
                DocumentoModificado = TextoOpcional(info, "numDocModificado"),
            };

            // Factura y nota de crédito: totalConImpuestos/totalImpuesto; nota de débito: impuestos/impuesto.
            var impuestos = (Hijo(info, "totalConImpuestos") ?? Hijo(info, "impuestos"))?.Elements() ?? Enumerable.Empty<XElement>();
            foreach (var impuesto in impuestos)
            {
                var valor = Decimal(impuesto, "valor");
                if (Texto(impuesto, "codigo") != "2")
                {
                    comprobante.OtrosImpuestos += valor; // ICE (3) o IRBPNR (5)
                    continue;
                }
                // codigoPorcentaje IVA: 0 = 0 %, 6 = no objeto, 7 = exento; el resto son tarifas gravadas.
                if (Texto(impuesto, "codigoPorcentaje") is "0" or "6" or "7")
                    comprobante.BaseIva0 += Decimal(impuesto, "baseImponible");
                else
                    comprobante.BaseIvaGravada += Decimal(impuesto, "baseImponible");
                comprobante.Iva += valor;
            }

            comprobante.Detalles = tipo == TiposComprobante.NotaDebito ? MotivosNotaDebito(raiz) : Detalles(raiz);
            return comprobante;
        }

        private static List<ComprobanteDetalle> Detalles(XElement raiz) =>
            (Hijo(raiz, "detalles")?.Elements() ?? Enumerable.Empty<XElement>())
            .Select(d => new ComprobanteDetalle
            {
                // La factura usa codigoPrincipal; la nota de crédito, codigoInterno.
                Codigo = TextoOpcional(d, "codigoPrincipal") ?? TextoOpcional(d, "codigoInterno"),
                Descripcion = Recortar(Texto(d, "descripcion"), 500),
                Cantidad = Decimal(d, "cantidad"),
                PrecioUnitario = Decimal(d, "precioUnitario"),
                Descuento = Decimal(d, "descuento"),
                Subtotal = Decimal(d, "precioTotalSinImpuesto"),
            })
            .ToList();

        private static List<ComprobanteDetalle> MotivosNotaDebito(XElement raiz) =>
            (Hijo(raiz, "motivos")?.Elements() ?? Enumerable.Empty<XElement>())
            .Select(m => new ComprobanteDetalle
            {
                Descripcion = Recortar(Texto(m, "razon"), 500),
                Cantidad = 1,
                PrecioUnitario = Decimal(m, "valor"),
                Subtotal = Decimal(m, "valor"),
            })
            .ToList();

        private static XElement? Hijo(XElement padre, string nombre) =>
            padre.Elements().FirstOrDefault(e => e.Name.LocalName == nombre);

        private static string? TextoOpcional(XElement padre, string nombre)
        {
            var valor = Hijo(padre, nombre)?.Value.Trim();
            return string.IsNullOrEmpty(valor) ? null : valor;
        }

        private static string Texto(XElement padre, string nombre) => TextoOpcional(padre, nombre) ?? string.Empty;

        private static decimal Decimal(XElement padre, string nombre) =>
            decimal.TryParse(TextoOpcional(padre, nombre), NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) ? valor : 0m;

        private static string Recortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];
    }
}
