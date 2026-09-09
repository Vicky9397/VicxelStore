using System.Text.Json;
using Dpm.BuildingBlocks.Domain;
using Dpm.BuildingBlocks.Infrastructure;
using Dpm.Orders.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dpm.Orders.Infrastructure.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);

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
