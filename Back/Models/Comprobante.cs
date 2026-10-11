namespace Back.Models
{
    /// <summary>
    /// Comprobante electrónico (factura, nota de crédito o débito) verificado contra el SRI.
    /// Si lo emitió el propio usuario es un ingreso; si no, un gasto.
    /// </summary>
    public class Comprobante
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        /// <summary>Se recalcula cuando el usuario cambia su RUC en el perfil.</summary>
        public Direccion Direccion { get; set; } = Direccion.Recibido;

        public string ClaveAcceso { get; set; } = string.Empty;
        public string TipoComprobante { get; set; } = string.Empty;
        public string NumeroComprobante { get; set; } = string.Empty;
        public string NumeroAutorizacion { get; set; } = string.Empty;
        public DateTime? FechaAutorizacion { get; set; }
        public DateOnly FechaEmision { get; set; }

        public string RucEmisor { get; set; } = string.Empty;
        public string RazonSocialEmisor { get; set; } = string.Empty;
        public string? NombreComercial { get; set; }
        public string IdentificacionComprador { get; set; } = string.Empty;
        public string RazonSocialComprador { get; set; } = string.Empty;

        public decimal Subtotal { get; set; }
        public decimal BaseIva0 { get; set; }
        public decimal BaseIvaGravada { get; set; }
        public decimal Iva { get; set; }
        public decimal OtrosImpuestos { get; set; }
        public decimal Descuento { get; set; }
        public decimal Propina { get; set; }
        public decimal Total { get; set; }

        /// <summary>Para notas de crédito/débito: número del documento que modifican.</summary>
        public string? DocumentoModificado { get; set; }

        public CategoriaGasto Categoria { get; set; } = CategoriaGasto.SinCategoria;
        public DateTime FechaImportacion { get; set; } = DateTime.UtcNow;

        public List<ComprobanteDetalle> Detalles { get; set; } = new();
        public ComprobanteXml? Xml { get; set; }

        /// <summary>Las notas de crédito restan del gasto o del ingreso.</summary>
        public int Signo => TipoComprobante == TiposComprobante.NotaCredito ? -1 : 1;
    }

    /// <summary>Recibido: el usuario es el comprador (gasto). Emitido: el usuario es el emisor (ingreso).</summary>
    public enum Direccion
    {
        Recibido,
        Emitido,
    }

    public class ComprobanteDetalle
    {
        public int Id { get; set; }
        public int ComprobanteId { get; set; }
        public string? Codigo { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Descuento { get; set; }
        public decimal Subtotal { get; set; }
    }

    /// <summary>XML autorizado original, en tabla aparte para no cargarlo en listados.</summary>
    public class ComprobanteXml
    {
        public int ComprobanteId { get; set; }
        public string Contenido { get; set; } = string.Empty;
    }

    /// <summary>Categoría de gasto asignada a un proveedor; se aplica a sus comprobantes al importarlos.</summary>
    public class ProveedorCategoria
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string Ruc { get; set; } = string.Empty;
        public CategoriaGasto Categoria { get; set; }
    }

    /// <summary>Rubros de gastos personales deducibles del Impuesto a la Renta (más dos de control).</summary>
    public enum CategoriaGasto
    {
        SinCategoria,
        Vivienda,
        Educacion,
        Salud,
        Alimentacion,
        Vestimenta,
        Turismo,
        NoDeducible,
    }

    public static class TiposComprobante
    {
        public const string Factura = "01";
        public const string NotaCredito = "04";
        public const string NotaDebito = "05";

        public static readonly IReadOnlySet<string> Soportados = new HashSet<string> { Factura, NotaCredito, NotaDebito };

        public static string Nombre(string codigo) => codigo switch
        {
            Factura => "Factura",
            NotaCredito => "Nota de crédito",
            NotaDebito => "Nota de débito",
            "03" => "Liquidación de compra",
            "06" => "Guía de remisión",
            "07" => "Comprobante de retención",
            _ => $"Tipo {codigo}",
        };
    }
}
