using System.Security.Cryptography;

namespace Amazoff.Api.Features.Auth;

public static class PasswordService
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private const string Prefix = "pbkdf2";

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        return string.Join(
            '$',
            Prefix,
            Iterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public static bool VerifyPassword(string password, string storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return false;
        }

        var parts = storedValue.Split('$', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 4 || !string.Equals(parts[0], Prefix, StringComparison.Ordinal))
        {
            return string.Equals(password, storedValue, StringComparison.Ordinal);
        }

        if (!int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[2]);
        var expectedHash = Convert.FromBase64String(parts[3]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public static bool NeedsRehash(string storedValue)
    {
        return !storedValue.StartsWith($"{Prefix}$", StringComparison.Ordinal);
    }
}
