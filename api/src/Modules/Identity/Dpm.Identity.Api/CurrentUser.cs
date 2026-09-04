using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dpm.Identity.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Dpm.Identity.Api;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? PublicId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            var sub = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }
}
