using System.Text.Json;
using Dpm.BuildingBlocks.Domain;
using Dpm.BuildingBlocks.Infrastructure;
using Dpm.Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dpm.Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<ProductModeration> ProductModerations => Set<ProductModeration>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var aggregate in ChangeTracker.Entries<AggregateRoot>()
                     .Where(e => e.Entity.DomainEvents.Count > 0)
                     .Select(e => e.Entity)
                     .ToList())
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                OutboxMessages.Add(new OutboxMessage
                {
                    EventId = domainEvent.EventId,
                    Type = domainEvent.GetType().AssemblyQualifiedName!,
                    PayloadJson = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    OccurredAtUtc = domainEvent.OccurredAtUtc,
                });
            }

            aggregate.ClearDomainEvents();
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Persistence record for a moderator's decision (catalog.ProductModerations).</summary>
public sealed class ProductModeration
{
    public long Id { get; init; }

    public required long ProductId { get; init; }

    public required long ModeratorUserId { get; init; }

    public required byte Decision { get; init; }

    public string? Reason { get; init; }

    public required DateTime CreatedAtUtc { get; init; }
}
