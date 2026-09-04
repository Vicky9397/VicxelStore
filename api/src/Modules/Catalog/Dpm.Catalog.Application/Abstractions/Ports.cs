using Dpm.Catalog.Domain;

namespace Dpm.Catalog.Application.Abstractions;

public interface IProductRepository
{
    Task<Product?> FindByPublicIdAsync(Guid publicId, CancellationToken ct);

    Task<Product?> FindBySlugAsync(string slug, CancellationToken ct);

    Task<Product?> FindByVariantPublicIdAsync(Guid variantPublicId, CancellationToken ct);

    Task<Product?> FindByIdAsync(long productId, CancellationToken ct);

    Task<Product?> FindByVariantIdAsync(long variantId, CancellationToken ct);

    Task<bool> SlugExistsAsync(string slug, CancellationToken ct);

    void Add(Product product);
}

public interface ICategoryRepository
{
    Task<Category?> FindBySlugAsync(string slug, CancellationToken ct);

    Task<Category?> FindByIdAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<Category>> ListAsync(CancellationToken ct);
}

public interface ITagRepository
{
    Task<Tag?> FindByNameAsync(string name, CancellationToken ct);

    void Add(Tag tag);
}

public interface ICatalogUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Read-side queries for the public storefront, served without loading aggregates.</summary>
public interface IProductQueries
{
    Task<Contracts.ProductPage> SearchAsync(Contracts.ProductSearch search, CancellationToken ct);

    Task<Contracts.ProductDetailDto?> FindPublishedBySlugAsync(string slug, CancellationToken ct);

    Task<IReadOnlyList<Contracts.SellerProductDto>> ListForStoreAsync(long storeId, CancellationToken ct);

    Task<IReadOnlyList<Contracts.ModerationQueueItemDto>> ListModerationQueueAsync(CancellationToken ct);
}

/// <summary>Records a moderator's decision (catalog.ProductModerations).</summary>
public interface IModerationLog
{
    void Record(long productId, long moderatorUserId, bool approved, string? reason);
}
