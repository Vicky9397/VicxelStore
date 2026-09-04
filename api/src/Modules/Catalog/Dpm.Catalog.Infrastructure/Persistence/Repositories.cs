using Dpm.BuildingBlocks.Domain;
using Dpm.Catalog.Application.Abstractions;
using Dpm.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dpm.Catalog.Infrastructure.Persistence;

public sealed class ProductRepository(CatalogDbContext dbContext) : IProductRepository
{
    private IQueryable<Product> WithAggregate() =>
        dbContext.Products
            .Include(p => p.Variants)
            .Include(p => p.Versions)
            .Include(p => p.Tags);

    public Task<Product?> FindByPublicIdAsync(Guid publicId, CancellationToken ct) =>
        WithAggregate().FirstOrDefaultAsync(p => p.PublicId == publicId, ct);

    public Task<Product?> FindBySlugAsync(string slug, CancellationToken ct) =>
        WithAggregate().FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public Task<Product?> FindByVariantPublicIdAsync(Guid variantPublicId, CancellationToken ct) =>
        WithAggregate().FirstOrDefaultAsync(p => p.Variants.Any(v => v.PublicId == variantPublicId), ct);

    public Task<Product?> FindByIdAsync(long productId, CancellationToken ct) =>
        WithAggregate().FirstOrDefaultAsync(p => p.Id == productId, ct);

    public Task<Product?> FindByVariantIdAsync(long variantId, CancellationToken ct) =>
        WithAggregate().FirstOrDefaultAsync(p => p.Variants.Any(v => v.Id == variantId), ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct) =>
        dbContext.Products.IgnoreQueryFilters().AnyAsync(p => p.Slug == slug, ct);

    public void Add(Product product) => dbContext.Products.Add(product);
}

public sealed class CategoryRepository(CatalogDbContext dbContext) : ICategoryRepository
{
    public Task<Category?> FindBySlugAsync(string slug, CancellationToken ct) =>
        dbContext.Categories.FirstOrDefaultAsync(c => c.Slug == slug, ct);

    public Task<Category?> FindByIdAsync(int id, CancellationToken ct) =>
        dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Category>> ListAsync(CancellationToken ct) =>
        await dbContext.Categories.OrderBy(c => c.Name).ToListAsync(ct);
}

public sealed class TagRepository(CatalogDbContext dbContext) : ITagRepository
{
    public Task<Tag?> FindByNameAsync(string name, CancellationToken ct) =>
        dbContext.Tags.FirstOrDefaultAsync(t => t.Name == name, ct);

    public void Add(Tag tag) => dbContext.Tags.Add(tag);
}

public sealed class CatalogUnitOfWork(CatalogDbContext dbContext) : ICatalogUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}

public sealed class ModerationLog(CatalogDbContext dbContext, IClock clock) : IModerationLog
{
    public void Record(long productId, long moderatorUserId, bool approved, string? reason) =>
        dbContext.ProductModerations.Add(new ProductModeration
        {
            ProductId = productId,
            ModeratorUserId = moderatorUserId,
            Decision = approved ? (byte)1 : (byte)2,
            Reason = reason,
            CreatedAtUtc = clock.UtcNow,
        });
}
