using Dpm.Identity.Application.Abstractions;
using Dpm.Identity.Contracts;

namespace Dpm.Identity.Infrastructure;

public sealed class UserDirectory(IUserRepository users) : IUserDirectory
{
    public async Task<UserRef?> FindByPublicIdAsync(Guid userPublicId, CancellationToken ct)
    {
        var user = await users.FindByPublicIdAsync(userPublicId, ct);
        return user is null ? null : new UserRef(user.Id, user.PublicId, user.EmailVerified);
    }
}
