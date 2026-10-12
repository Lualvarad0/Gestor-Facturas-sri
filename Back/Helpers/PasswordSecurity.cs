using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace Back.Helpers;

public static class PasswordSecurity
{
    public const int Iterations = 600_000;
    public static bool IsAllowed(string password) => password.Length is >= 15 and <= 128
        && !string.IsNullOrWhiteSpace(password)
        && !new[] { "123456789012345", "passwordpassword", "contraseñacontraseña" }.Contains(password.ToLowerInvariant());

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        // Permite iniciar sesión con las cuentas BCrypt anteriores sin cambiar sus contraseñas.
        if (stored.StartsWith("$2")) return BCrypt.Net.BCrypt.Verify(password, stored);
        try
        {
            var parts = stored.Split('$');
            if (parts.Length != 3 || parts[0] != "pbkdf2") return false;
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(parts[1]), Iterations, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(parts[2]));
        }
        catch (FormatException) { return false; }
    }

    public static string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>
    /// Si la contraseña contiene nombres, apellidos, el correo (parte local) o la fecha de nacimiento.
    /// Compara sin mayúsculas, tildes ni separadores; ignora palabras de menos de 4 letras (de, la, Ana…).
    /// Web/src/app/shared/password.ts aplica la misma regla.
    /// </summary>
    public static bool ContieneDatosPersonales(string password, string? nombres, string? apellidos, string? correo, DateOnly? nacimiento)
    {
        var clave = Normalizar(password);
        var local = correo?.Split('@')[0];
        var fragmentos = new[] { nombres, apellidos, local }
            .SelectMany(t => System.Text.RegularExpressions.Regex.Split(t ?? string.Empty, @"[^\p{L}\p{N}]+"))
            .Append(local)
            .Select(t => Normalizar(t ?? string.Empty))
            .Where(t => t.Length >= 4);
        if (nacimiento is { } f)
            fragmentos = fragmentos.Concat(new[] { "yyyy", "ddMMyyyy", "yyyyMMdd", "MMddyyyy", "ddMMyy" }
                .Select(formato => f.ToString(formato, System.Globalization.CultureInfo.InvariantCulture)));
        return fragmentos.Any(clave.Contains);
    }

    private static string Normalizar(string texto)
    {
        var sb = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }
}

/// <summary>Fecha de nacimiento de una persona de 18 a 120 años.</summary>
public sealed class MayorDeEdadAttribute : ValidationAttribute
{
    public MayorDeEdadAttribute() => ErrorMessage = "Debes ser mayor de 18 años. Revisa tu fecha de nacimiento.";
    public override bool IsValid(object? value)
    {
        if (value is not DateOnly fecha) return value is null;
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        return fecha <= hoy.AddYears(-18) && fecha >= hoy.AddYears(-120);
    }
}

public sealed class SecurePasswordAttribute : ValidationAttribute
{
    public SecurePasswordAttribute() => ErrorMessage = "Usa entre 15 y 128 caracteres y evita contraseñas comunes. Puedes usar una frase larga con espacios.";
    public override bool IsValid(object? value) => value is string password && PasswordSecurity.IsAllowed(password);
}
