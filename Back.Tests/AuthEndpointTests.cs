using System.Net;
using System.Net.Http.Json;
using Back.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Back.Tests;

// Las solicitudes inválidas se rechazan antes de consultar la base de datos.
public class AuthEndpointTests
{
    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Default", "Host=localhost;Database=unused;Username=unused");
            builder.UseSetting("Jwt:Key", new string('k', 48));
            builder.UseSetting("Database:MigrateOnStartup", "false");
        }
    }

    [Fact]
    public async Task LoginYRegistro_RechazanDatosInvalidosYLimitanIntentos()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        for (var i = 0; i < 10; i++)
        {
            var route = i % 2 == 0 ? "login" : "register";
            var response = await client.PostAsJsonAsync($"/api/auth/{route}", new { nombres = "Usuario", apellidos = "Prueba", fechaNacimiento = "1990-05-15", email = "invalid", password = "short" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }
        var limited = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "yo@example.ec" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
    }

    [Fact]
    public async Task Reset_RechazaTokenInvalidoYClaveDebil()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = "yo@example.ec", token = new string('a', 64), password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        response = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = "yo@example.ec", token = "bad", password = "una frase suficientemente larga" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recuperacion_SinProveedorConfigurado_InformaIndisponibilidad()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "yo@example.ec" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("token", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
}
