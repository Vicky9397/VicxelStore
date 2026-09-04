namespace Dpm.Identity.Contracts;

/// <summary>
/// The Identity module's public surface. Downstream modules resolve users
/// through this instead of reading identity.* directly. Identity is upstream of
/// every other module (spec 04 section 4.7), so depending on this is allowed.
/// </summary>
public interface IUserDirectory
{
    Task<UserRef?> FindByPublicIdAsync(Guid userPublicId, CancellationToken ct);
}

/// <param name="UserId">Internal id, used for foreign keys inside the API and never exposed.</param>
public sealed record UserRef(long UserId, Guid PublicId, bool EmailVerified);
