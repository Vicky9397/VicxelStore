using System.Security.Cryptography;
using Dpm.Identity.Application.Abstractions;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace Dpm.Identity.Infrastructure.Security;

/// <summary>
/// PBKDF2-HMACSHA256 hasher (spec 08 section 8.2). Format:
/// pbkdf2-sha256.{iterations}.{salt-base64}.{hash-base64}
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Prefix = "pbkdf2-sha256";
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, Iterations, KeySize);
        return string.Join('.', Prefix, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, iterations, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
