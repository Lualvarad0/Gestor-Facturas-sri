using Back.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Back.Helpers
{
    public class JwtOptions
    {
        public const string Section = "Jwt";

        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = "gestionador-facturas";
        public string Audience { get; set; } = "gestionador-facturas";
        public int ExpirationHours { get; set; } = 8;

        public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));
    }

    public static class JwtHelper
    {
        public static (string Token, DateTime ExpiraEn) GenerateToken(JwtOptions options, Usuario usuario)
        {
            var expiraEn = DateTime.UtcNow.AddHours(options.ExpirationHours);
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, usuario.Correo),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.Name, usuario.Nombre),
                new(ClaimTypes.Role, usuario.Rol),
            };

            var token = new JwtSecurityToken(
                issuer: options.Issuer,
                audience: options.Audience,
                claims: claims,
                expires: expiraEn,
                signingCredentials: new SigningCredentials(options.SigningKey(), SecurityAlgorithms.HmacSha256));

            return (new JwtSecurityTokenHandler().WriteToken(token), expiraEn);
        }

        public static int GetUsuarioId(ClaimsPrincipal user) =>
            int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    }
}
