using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dpm.Identity.Contracts;
using Dpm.Orders.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Dpm.Orders.Api;

public sealed class CurrentBuyer(
    IHttpContextAccessor httpContextAccessor,
    IUserDirectory users)
    : ICurrentBuyer
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
