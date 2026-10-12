using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Back.Data;
using Back.Helpers;
using Back.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Back.Tests;

public class FakeRecoveryMail : IRecoveryMail
{
    public bool Configured => true;
    public ConcurrentDictionary<string, string> Tokens { get; } = new();
    public int Enviados;
    public bool Enqueue(string email, string token)
    {
        Tokens[email] = token;
        Interlocked.Increment(ref Enviados);
        return true;
    }
}

public partial class ImportacionSriTests
{
    [Fact]
    public async Task Recuperacion_CambiaClaveRevocaSesionesYRechazaReutilizacion()
    {
        const string email = "recover@test.ec";
        using var http = await ClienteAutenticadoAsync(email);
        var response = await http.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = _factory.Recovery.Tokens[email];
        Assert.Equal(64, token.Length);
        using (var scope = _factory.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Usuarios.SingleAsync(u => u.Correo == email);
            Assert.Equal(PasswordSecurity.Fingerprint(token), user.ResetTokenHash);
            Assert.NotEqual(token, user.ResetTokenHash);
            Assert.True(user.ResetExpiresAt > DateTime.UtcNow);
        }
        var request = new { email, token, password = "Mi nueva frase privada 456" };
        // Dos solicitudes simultáneas: una sola puede consumir el enlace.
        var responses = await Task.WhenAll(
            http.PostAsJsonAsync("/api/auth/reset-password", request),
            http.PostAsJsonAsync("/api/auth/reset-password", request));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/perfil")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Mi frase secreta 123" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsJsonAsync("/api/auth/login", request)).StatusCode);
    }

    [Fact]
    public async Task Recuperacion_RespuestaGenericaYEsperaEntreCorreos()
    {
        const string email = "cooldown@test.ec";
        using var http = await ClienteAutenticadoAsync(email);
        var known = await http.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var token = _factory.Recovery.Tokens[email];
        var sent = _factory.Recovery.Enviados;
        var unknown = await http.PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody@test.ec" });
        var repeat = await http.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.Equal(await known.Content.ReadAsStringAsync(), await repeat.Content.ReadAsStringAsync());
        Assert.Equal(sent, _factory.Recovery.Enviados);
        Assert.Equal(token, _factory.Recovery.Tokens[email]);
    }

    [Fact]
    public async Task Recuperacion_RechazaEnlaceCaducado()
    {
        const string email = "expired@test.ec";
        using var http = await ClienteAutenticadoAsync(email);
        await http.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Usuarios.Where(u => u.Correo == email)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ResetExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        var response = await http.PostAsJsonAsync("/api/auth/reset-password",
            new { email, token = _factory.Recovery.Tokens[email], password = "Mi nueva frase privada 456" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Pedir recuperación no bloquea ni altera la contraseña actual.
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Mi frase secreta 123" })).StatusCode);
    }

    [Fact]
    public async Task Login_BloqueaTrasCincoFallosYPermiteEntrarAlCaducar()
    {
        const string email = "lockout@test.ec";
        using var http = await ClienteAutenticadoAsync(email);
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsJsonAsync("/api/auth/login",
                new { email, password = "incorrecta" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Mi frase secreta 123" })).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Usuarios.SingleAsync(u => u.Correo == email);
        Assert.True(user.LockoutUntil > DateTime.UtcNow);
        Assert.Equal("user", user.Rol);
        await db.Usuarios.Where(u => u.Correo == email).ExecuteUpdateAsync(s => s.SetProperty(u => u.LockoutUntil, DateTime.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.OK, (await http.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Mi frase secreta 123" })).StatusCode);
    }
}
