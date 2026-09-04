using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dpm.Catalog.Application.Moderation;
using Dpm.Identity.Contracts;
using Microsoft.AspNetCore.Http;

namespace Dpm.Catalog.Api;

/// <summary>Resolves the acting staff member so moderation decisions are attributable.</summary>
public sealed class CurrentModerator(
    IHttpContextAccessor httpContextAccessor,
    IUserDirectory users)
    : ICurrentModerator
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
}
