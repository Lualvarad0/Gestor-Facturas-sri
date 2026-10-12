using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using Back.Dtos;
using Back.Helpers;
using Back.Models;

namespace Back.Tests;

public class AuthSecurityTests
{
    [Theory]
    [InlineData("corta123", false)]
    [InlineData("               ", false)]
    [InlineData("123456789012345", false)]
    [InlineData("una frase única de acceso", true)]
    [InlineData("Mi frase segura de 2026", true)]
    public void Politica_AceptaFrasesYRechazaClavesDebiles(string password, bool expected) =>
        Assert.Equal(expected, PasswordSecurity.IsAllowed(password));

    [Fact]
    public void Hash_UsaSalAleatoriaYVerificaTodaLaClave()
    {
        // Comprueba que caracteres después del límite antiguo de BCrypt no se truncan.
        var password = new string('a', 80) + "á contraseña";
        var hash = PasswordSecurity.Hash(password);
        Assert.NotEqual(hash, PasswordSecurity.Hash(password));
        Assert.DoesNotContain(password, hash);
        Assert.True(PasswordSecurity.Verify(password, hash));
        Assert.False(PasswordSecurity.Verify(new string('a', 80) + "otra contraseña", hash));
    }

    [Fact]
    public void Login_ConservaCompatibilidadConBcrypt()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("Secreta123");
        Assert.True(PasswordSecurity.Verify("Secreta123", hash));
        Assert.False(PasswordSecurity.Verify("incorrecta", hash));
    }

    [Fact]
    public void RegistroYRestablecimiento_AplicanMismaPoliticaEnServidor()
    {
        foreach (var password in new[] { "corta", new string('x', 129), "               " })
        {
            Assert.False(Valid(new RegisterRequest("Usuario", "yo@example.ec", password)));
            Assert.False(Valid(new ResetPasswordRequest("yo@example.ec", new string('a', 64), password)));
        }
        Assert.True(Valid(new RegisterRequest("Usuario", "yo@example.ec", "una frase segura y única")));
        Assert.False(Valid(new ResetPasswordRequest("yo@example.ec", "token inválido", "una frase segura y única")));
        Assert.False(Valid(new RegisterRequest("  ", "yo@example.ec", "una frase segura y única")));
    }

    [Fact]
    public void Jwt_SelloDeSesionCambiaConLaContraseña()
    {
        var user = new Usuario { Id = 1, Correo = "yo@example.ec", PasswordHash = PasswordSecurity.Hash("mi frase segura original") };
        var options = new JwtOptions { Key = new string('k', 48) };
        var token = new JwtSecurityTokenHandler().ReadJwtToken(JwtHelper.GenerateToken(options, user).Token);
        var stamp = token.Claims.Single(c => c.Type == "pwd").Value;
        Assert.Equal(PasswordSecurity.Fingerprint(user.PasswordHash), stamp);
        user.PasswordHash = PasswordSecurity.Hash("mi frase segura actualizada");
        Assert.NotEqual(PasswordSecurity.Fingerprint(user.PasswordHash), stamp);
        Assert.True(token.ValidTo > DateTime.UtcNow);
    }

    // MVC valida los atributos de los parámetros del constructor de estos records.
    private static bool Valid(object value) => value.GetType().GetConstructors().Single().GetParameters().All(p =>
        p.GetCustomAttributes(typeof(ValidationAttribute), true).Cast<ValidationAttribute>()
            .All(a => a.IsValid(value.GetType().GetProperty(p.Name!)!.GetValue(value))));
}
