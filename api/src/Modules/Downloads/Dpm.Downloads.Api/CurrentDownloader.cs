using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Dpm.Downloads.Application.Abstractions;
using Dpm.Identity.Contracts;
using Microsoft.AspNetCore.Http;

namespace Dpm.Downloads.Api;

public sealed class CurrentDownloader(
    IHttpContextAccessor httpContextAccessor,
    IUserDirectory users)
    : ICurrentDownloader
{
    public async Task<long?> ResolveUserIdAsync(CancellationToken ct)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var sub = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var publicId))
        {
            return null;
        }

        var user = await users.FindByPublicIdAsync(publicId, ct);
        return user?.UserId;
    }

    /// <summary>
    /// The audit trail records a hash rather than the address itself, so the log
    /// keeps its forensic value without storing personal data (spec 08 section 8.11).
    /// </summary>
    public string? ClientIpHash
    {
        get
        {
            var address = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            return address is null
                ? null
                : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(address))).ToLowerInvariant();
        }
    }
}
