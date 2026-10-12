using System.ComponentModel.DataAnnotations;
using Back.Helpers;

namespace Back.Dtos
{
    public record RegisterRequest(
        [Required, StringLength(100, MinimumLength = 2), RegularExpression(@"^\S(?:[\s\S]*\S)$")] string Username,
        [Required, EmailAddress, StringLength(150)] string Email,
        [Required, SecurePassword] string Password);

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
