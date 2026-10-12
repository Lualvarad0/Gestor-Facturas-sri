namespace Back.Models
{
    /// <summary>Rubro de gasto que define el usuario (luz, internet, gimnasio…) con su límite mensual.</summary>
    public class Rubro
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal Limite { get; set; }
        /// <summary>Posición en la lista, tal como la ordenó el usuario.</summary>
        public int Orden { get; set; }
    }

    /// <summary>Rubro asignado a un proveedor: sus comprobantes cuentan contra el límite de ese rubro.</summary>
    public class ProveedorRubro
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string Ruc { get; set; } = string.Empty;
        public int RubroId { get; set; }
    }
}
