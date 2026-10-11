namespace Back.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Rol { get; set; } = Roles.User;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        // Perfil tributario
        public string? Cedula { get; set; }
        public string? Ruc { get; set; }
        /// <summary>Nombre o razón social tal como consta en el RUC.</summary>
        public string? RazonSocial { get; set; }
        public RegimenTributario? Regimen { get; set; }
        public int CargasFamiliares { get; set; }
        public bool EnfermedadCatastrofica { get; set; }

        /// <summary>Identificaciones con las que el usuario aparece como comprador en sus facturas.</summary>
        public IEnumerable<string> Identificaciones()
        {
            if (!string.IsNullOrEmpty(Cedula)) yield return Cedula;
            if (!string.IsNullOrEmpty(Ruc)) yield return Ruc;
        }
    }

    public static class Roles
    {
        public const string Admin = "admin";
        public const string User = "user";
    }

    public enum RegimenTributario
    {
        General,
        RimpeEmprendedor,
        RimpeNegocioPopular,
    }
}
