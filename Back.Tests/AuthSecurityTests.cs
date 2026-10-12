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
            Assert.False(Valid(Registro(password: password)));
            Assert.False(Valid(new ResetPasswordRequest("yo@example.ec", new string('a', 64), password)));
        }
        Assert.True(Valid(Registro()));
        Assert.False(Valid(new ResetPasswordRequest("yo@example.ec", "token inválido", "una frase segura y única")));
        Assert.False(Valid(Registro(nombres: "  ")));
        Assert.False(Valid(Registro(apellidos: "Pérez 2")));
        Assert.True(Valid(Registro(nombres: "María José", apellidos: "O'Neil-Pérez")));
    }

    [Fact]
    public void Registro_ExigeMayoriaDeEdad()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.False(Valid(Registro(nacimiento: hoy.AddYears(-18).AddDays(1))));
        Assert.True(Valid(Registro(nacimiento: hoy.AddYears(-18))));
        Assert.False(Valid(Registro(nacimiento: hoy.AddYears(-121))));
        Assert.False(Valid(new RegisterRequest("David", "Alvarado", null, "yo@example.ec", "una frase segura y única")));
    }

    [Theory]
    [InlineData("mi frase con david adentro", true)]
    [InlineData("Mi frase de ÁLVARADO segura", true)]
    [InlineData("frase segura y única 1990", true)]
    [InlineData("frase segura 15/05/1990 aquí", true)]
    [InlineData("frase con mi correo yo.cuenta", true)]
    [InlineData("una frase segura y única", false)]
    [InlineData("frase con de la y ana corta", false)]
    public void Clave_RechazaDatosPersonales(string password, bool contiene) =>
        Assert.Equal(contiene, PasswordSecurity.ContieneDatosPersonales(password, "David Ana", "Alvarado de la Cruz", "yo.cuenta@example.ec", new DateOnly(1990, 5, 15)));

    [Fact]
    public void Registro_ValidaDatosPersonalesEnLaClave()
    {
        var conNombre = Registro(password: "la frase segura de david");
        Assert.Contains(conNombre.Validate(new ValidationContext(conNombre)), r => r.MemberNames.Contains("Password"));
        var segura = Registro();
        Assert.Empty(segura.Validate(new ValidationContext(segura)));
    }

    private static RegisterRequest Registro(string nombres = "David", string apellidos = "Alvarado",
        DateOnly? nacimiento = null, string password = "una frase segura y única") =>
        new(nombres, apellidos, nacimiento ?? new DateOnly(1990, 5, 15), "yo@example.ec", password);

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
