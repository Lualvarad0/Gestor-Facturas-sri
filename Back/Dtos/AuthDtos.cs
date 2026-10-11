using System.ComponentModel.DataAnnotations;

namespace Back.Dtos
{
    public record RegisterRequest(
        [Required, StringLength(100, MinimumLength = 2)] string Username,
        [Required, EmailAddress] string Email,
        [Required, StringLength(100, MinimumLength = 8)] string Password);

    public record LoginRequest(
        [Required, EmailAddress] string Email,
        [Required] string Password);

    public record AuthResponse(string Token, DateTime ExpiraEn, UsuarioDto Usuario);
}
