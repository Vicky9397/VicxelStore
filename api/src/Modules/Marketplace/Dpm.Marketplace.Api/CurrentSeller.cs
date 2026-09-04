using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dpm.Identity.Contracts;
using Dpm.Marketplace.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Dpm.Marketplace.Api;

/// <summary>
/// Resolves the caller from the bearer token. The internal user id comes from
/// the Identity module through its public directory; Marketplace never reads
/// identity.* itself.
/// </summary>
public sealed class CurrentSeller(
    IHttpContextAccessor httpContextAccessor,
    IUserDirectory users)
    : ICurrentSeller
{
    public Guid? UserPublicId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            var sub = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public async Task<long?> ResolveUserIdAsync(CancellationToken ct)
    {
        if (UserPublicId is not { } publicId)
        {
            return null;
        }

        var user = await users.FindByPublicIdAsync(publicId, ct);
        return user?.UserId;
    }
}
