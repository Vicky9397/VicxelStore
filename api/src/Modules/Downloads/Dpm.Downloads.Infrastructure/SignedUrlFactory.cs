using System.Security.Cryptography;
using System.Text;
using System.Web;
using Dpm.BuildingBlocks.Domain;
using Dpm.Downloads.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Dpm.Downloads.Infrastructure;

public sealed class DownloadOptions
{
    public const string SectionName = "Downloads";

    /// <summary>Base URL the signed link points at: the CDN in front of object storage.</summary>
    public string BaseUrl { get; init; } = "http://localhost:5080/api/v1/downloads/object";

    /// <summary>
    /// Signing key for download URLs. Never the JWT key: a leaked download key
    /// must not be usable to mint access tokens.
    /// </summary>
    public string SigningKey { get; init; } = string.Empty;
}

/// <summary>
/// Mints short-lived signed URLs (spec 08 section 8.6). The signature covers the
/// storage key and the expiry, so a URL cannot be edited to reach another object
/// or to outlive its window, and the raw storage key never reaches the client
/// unsigned.
/// </summary>
public sealed class SignedUrlFactory(IOptions<DownloadOptions> options, IClock clock) : ISignedUrlFactory
{
    private readonly DownloadOptions _options = options.Value;

    public SignedUrl Create(string storageKey, string fileName, TimeSpan lifetime)
    {
        var expiresAt = clock.UtcNow.Add(lifetime);
        var expiresUnix = new DateTimeOffset(expiresAt, TimeSpan.Zero).ToUnixTimeSeconds();
        var signature = Sign($"{storageKey}\n{expiresUnix}");

        var url =
            $"{_options.BaseUrl.TrimEnd('/')}" +
            $"?key={HttpUtility.UrlEncode(storageKey)}" +
            $"&name={HttpUtility.UrlEncode(fileName)}" +
            $"&expires={expiresUnix}" +
            $"&sig={signature}";

        return new SignedUrl(url, (int)lifetime.TotalSeconds);
    }

    /// <summary>Re-checks a presented URL. The expiry is verified before the signature is trusted.</summary>
    public bool Verify(string storageKey, long expiresUnix, string signature)
    {
        if (DateTimeOffset.FromUnixTimeSeconds(expiresUnix).UtcDateTime <= clock.UtcNow)
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(Sign($"{storageKey}\n{expiresUnix}"));
        var presented = Encoding.UTF8.GetBytes(signature);
        return expected.Length == presented.Length
            && CryptographicOperations.FixedTimeEquals(expected, presented);
    }

    private string Sign(string payload)
    {
        var key = string.IsNullOrWhiteSpace(_options.SigningKey)
            ? "download-development-signing-key"
            : _options.SigningKey;
        return Convert.ToHexString(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
    }
}
