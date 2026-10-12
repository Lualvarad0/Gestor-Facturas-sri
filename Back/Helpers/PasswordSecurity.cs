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
}

public sealed class SecurePasswordAttribute : ValidationAttribute
{
    public SecurePasswordAttribute() => ErrorMessage = "Usa entre 15 y 128 caracteres y evita contraseñas comunes. Puedes usar una frase larga con espacios.";
    public override bool IsValid(object? value) => value is string password && PasswordSecurity.IsAllowed(password);
}
