using System.Text.Json;
using Dpm.BuildingBlocks.Domain;
using Dpm.BuildingBlocks.Infrastructure;
using Dpm.Marketplace.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dpm.Marketplace.Infrastructure.Persistence;

public sealed class MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options) : DbContext(options)
{
    public DbSet<Store> Stores => Set<Store>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MarketplaceDbContext).Assembly);

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
