using System.ComponentModel.DataAnnotations;
using Back.Helpers;

namespace Back.Dtos
{
    public record RegisterRequest(
        [Required, StringLength(60, MinimumLength = 2), RegularExpression(DatosPersonales.PatronNombre)] string Nombres,
        [Required, StringLength(60, MinimumLength = 2), RegularExpression(DatosPersonales.PatronNombre)] string Apellidos,
        [Required, MayorDeEdad] DateOnly? FechaNacimiento,
        [Required, EmailAddress, StringLength(150)] string Email,
        [Required, SecurePassword] string Password) : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (PasswordSecurity.ContieneDatosPersonales(Password, Nombres, Apellidos, Email, FechaNacimiento))
                yield return new ValidationResult(DatosPersonales.MensajeClave, new[] { nameof(Password) });
        }
    }

    public static class DatosPersonales
    {
        /// <summary>Letras (con tildes), espacios, apóstrofos, puntos y guiones; empieza y termina en letra o punto.</summary>
        public const string PatronNombre = @"^\p{L}(?:[\p{L}\p{M}'’ .-]*[\p{L}\p{M}.])?$";
        public const string MensajeClave = "La contraseña no debe contener tus nombres, apellidos, correo ni fecha de nacimiento.";
    }

    public record LoginRequest(
        [Required, EmailAddress, StringLength(150)] string Email,
        [Required, StringLength(128)] string Password);

    public record ForgotPasswordRequest([Required, EmailAddress, StringLength(150)] string Email);
    public record ResetPasswordRequest(
        [Required, EmailAddress, StringLength(150)] string Email,
        [Required, StringLength(64, MinimumLength = 64), RegularExpression("^[a-fA-F0-9]{64}$")] string Token,
        [Required, SecurePassword] string Password);

    public record AuthResponse(string Token, DateTime ExpiraEn, UsuarioDto Usuario);
}
