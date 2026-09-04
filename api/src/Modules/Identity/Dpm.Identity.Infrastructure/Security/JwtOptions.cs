namespace Dpm.Identity.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "dpm-api";

    public string Audience { get; init; } = "dpm-clients";

    public int AccessTokenMinutes { get; init; } = 15;

    public int RefreshTokenDays { get; init; } = 14;

    /// <summary>PKCS#8 RSA private key PEM. When absent a dev-only ephemeral key is generated.</summary>
    public string? RsaPrivateKeyPem { get; init; }
}
