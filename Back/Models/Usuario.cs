namespace Back.Models
{
    public class Usuario
    {
        public int Id { get; set; }
        /// <summary>Nombre para mostrar: nombres y apellidos.</summary>
        public string Nombre { get; set; } = string.Empty;
        public string Nombres { get; set; } = string.Empty;
        /// <summary>Vacío en las cuentas creadas antes de pedir apellidos; se completa en Configuración.</summary>
        public string Apellidos { get; set; } = string.Empty;
        public DateOnly? FechaNacimiento { get; set; }
        public string Correo { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? ResetTokenHash { get; set; }
        public DateTime? ResetExpiresAt { get; set; }
        public DateTime? ResetRequestedAt { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockoutUntil { get; set; }
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

        // Presupuesto: ingreso aproximado del mes (los límites viven en Rubro)
        public decimal? IngresoMensual { get; set; }

        public static string NombreCompleto(string nombres, string apellidos) => $"{nombres} {apellidos}".Trim();

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
