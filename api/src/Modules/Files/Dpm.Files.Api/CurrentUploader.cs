using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dpm.Files.Application.Abstractions;
using Dpm.Identity.Contracts;
using Microsoft.AspNetCore.Http;

namespace Dpm.Files.Api;

public sealed class CurrentUploader(
    IHttpContextAccessor httpContextAccessor,
    IUserDirectory users)
    : ICurrentUploader
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
