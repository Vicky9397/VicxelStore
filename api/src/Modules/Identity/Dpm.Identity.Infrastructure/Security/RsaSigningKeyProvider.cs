using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dpm.Identity.Infrastructure.Security;

public interface IJwtSigningKeyProvider
{
    RsaSecurityKey SigningKey { get; }
}

/// <summary>
/// Holds the RS256 signing key (spec 08 section 8.2). Loads the configured PEM;
/// without one it generates an ephemeral key so local dev works out of the box
/// (tokens do not survive a restart; never acceptable beyond dev).
/// </summary>
public sealed partial class RsaSigningKeyProvider : IJwtSigningKeyProvider
{
    public RsaSecurityKey SigningKey { get; }

    public RsaSigningKeyProvider(IOptions<JwtOptions> options, ILogger<RsaSigningKeyProvider> logger)
    {
        var rsa = RSA.Create(2048);
        if (!string.IsNullOrWhiteSpace(options.Value.RsaPrivateKeyPem))
        {
            rsa.ImportFromPem(options.Value.RsaPrivateKeyPem);
        }
        else
        {
            LogEphemeralKey(logger);
        }

        SigningKey = new RsaSecurityKey(rsa);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Jwt:RsaPrivateKeyPem is not configured; using an ephemeral dev signing key")]
    private static partial void LogEphemeralKey(ILogger logger);
}
