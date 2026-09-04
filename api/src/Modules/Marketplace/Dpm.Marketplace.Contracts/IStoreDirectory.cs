namespace Dpm.Marketplace.Contracts;

/// <summary>
/// The Marketplace module's public surface. Downstream modules (Catalog) ask
/// store questions through this instead of reading market.* directly.
/// </summary>
public interface IStoreDirectory
{
    /// <summary>The signed-in user's store, or null when they do not have one.</summary>
    Task<StoreSummary?> FindForCurrentUserAsync(CancellationToken ct);

    Task<StoreSummary?> FindByIdAsync(long storeId, CancellationToken ct);
}

/// <param name="CanPublish">
/// Whether the store has cleared verification, tax and bank setup, which the
/// seller must complete before any product of theirs may be published
/// (spec 02 section 2.4 Seller Management).
/// </param>
public sealed record StoreSummary(
    long StoreId,
    Guid PublicId,
    string Slug,
    string Name,
    bool IsActive,
    bool CanPublish);
