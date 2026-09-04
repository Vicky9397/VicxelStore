using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Dpm.BuildingBlocks.Domain;
using Dpm.Identity.Application.Abstractions;
using Dpm.Identity.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dpm.Identity.Infrastructure.Security;

public sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    IJwtSigningKeyProvider keyProvider,
    IClock clock)
    : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public AccessToken IssueAccessToken(UserAccount user)
    {
        var now = clock.UtcNow;
        var expiresIn = TimeSpan.FromMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.PublicId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new("email_verified", user.EmailVerified ? "true" : "false"),
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r.Name)));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(expiresIn),
            signingCredentials: new SigningCredentials(keyProvider.SigningKey, SecurityAlgorithms.RsaSha256));

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            (int)expiresIn.TotalSeconds);
    }

    public (string Token, string Hash) GenerateOpaqueToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Base64UrlEncoder.Encode(bytes);
        return (token, HashOpaqueToken(token));
    }

    public string HashOpaqueToken(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant();
}
