using System.Globalization;
using System.Text.RegularExpressions;

namespace Back.Sri
{
    /// <summary>
    /// Clave de acceso de 49 dígitos (Ficha técnica de comprobantes electrónicos del SRI):
    /// fecha ddMMyyyy (8) | tipo comprobante (2) | RUC emisor (13) | ambiente (1) | serie (6) |
    /// secuencial (9) | código numérico (8) | tipo de emisión (1) | dígito verificador módulo 11 (1).
    /// </summary>
    public static partial class ClaveAcceso
    {
        public const int Longitud = 49;

        [GeneratedRegex(@"(?<!\d)\d{49}(?!\d)")]
        private static partial Regex Patron();

        /// <summary>
        /// Extrae todas las claves válidas de un texto cualquiera (reporte .txt del SRI, XML, texto pegado).
        /// Las secuencias de 49 dígitos que no cumplen el módulo 11 o la fecha se descartan.
        /// </summary>
        public static List<string> Extraer(string texto) =>
            Patron().Matches(texto).Select(m => m.Value).Where(EsValida).Distinct().ToList();

        public static bool EsValida(string clave) =>
            clave.Length == Longitud
            && clave.All(char.IsAsciiDigit)
            && DigitoVerificador(clave[..48]) == clave[48] - '0'
            && DateOnly.TryParseExact(clave[..8], "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

        /// <summary>Módulo 11 con pesos 2..7 de derecha a izquierda; 11 → 0 y 10 → 1.</summary>
        public static int DigitoVerificador(string digitos)
        {
            int suma = 0, factor = 2;
            for (var i = digitos.Length - 1; i >= 0; i--)
            {
                suma += (digitos[i] - '0') * factor;
                factor = factor == 7 ? 2 : factor + 1;
            }
            var resultado = 11 - suma % 11;
            return resultado switch { 11 => 0, 10 => 1, _ => resultado };
        }

        public static string TipoComprobante(string clave) => clave.Substring(8, 2);
        public static string RucEmisor(string clave) => clave.Substring(10, 13);
        public static string Ambiente(string clave) => clave.Substring(23, 1);
    }
}
