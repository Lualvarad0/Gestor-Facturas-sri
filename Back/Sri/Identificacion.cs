namespace Back.Sri
{
    /// <summary>Validación de cédula y RUC ecuatorianos.</summary>
    public static class Identificacion
    {
        /// <summary>
        /// Cédula: 10 dígitos, provincia 01–24 (o 30 para extranjeros), tercer dígito menor a 6
        /// y dígito verificador módulo 10 con coeficientes 2,1,2,1,…
        /// </summary>
        public static bool EsCedulaValida(string? cedula)
        {
            if (cedula is not { Length: 10 } || !cedula.All(char.IsAsciiDigit)) return false;
            if (!ProvinciaValida(cedula) || cedula[2] - '0' >= 6) return false;

            var suma = 0;
            for (var i = 0; i < 9; i++)
            {
                var producto = (cedula[i] - '0') * (i % 2 == 0 ? 2 : 1);
                suma += producto > 9 ? producto - 9 : producto;
            }
            return (10 - suma % 10) % 10 == cedula[9] - '0';
        }

        /// <summary>
        /// RUC: 13 dígitos con provincia válida. Persona natural (tercer dígito 0–5): cédula válida + establecimiento
        /// distinto de 000. Sociedades (6 pública, 9 privada): solo estructura, porque el SRI emite RUC de
        /// sociedades que ya no cumplen el antiguo dígito verificador módulo 11.
        /// </summary>
        public static bool EsRucValido(string? ruc)
        {
            if (ruc is not { Length: 13 } || !ruc.All(char.IsAsciiDigit) || !ProvinciaValida(ruc)) return false;
            return (ruc[2] - '0') switch
            {
                < 6 => EsCedulaValida(ruc[..10]) && ruc[10..] != "000",
                6 => ruc[9..] != "0000",
                9 => ruc[10..] != "000",
                _ => false,
            };
        }

        public static bool EsPersonaNatural(string ruc) => ruc.Length == 13 && ruc[2] - '0' < 6;

        private static bool ProvinciaValida(string numero)
        {
            var provincia = int.Parse(numero[..2]);
            return provincia is >= 1 and <= 24 or 30;
        }
    }
}
