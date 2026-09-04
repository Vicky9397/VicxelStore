using System.Text.Json;
using Dpm.BuildingBlocks.Domain;
using Dpm.BuildingBlocks.Infrastructure;
using Dpm.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dpm.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<UserToken> UserTokens => Set<UserToken>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }

    /// <summary>
    /// Persists raised domain events to the outbox in the same transaction as the
    /// aggregate change (spec 04 section 4.7).
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregates = ChangeTracker.Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var aggregate in aggregates)
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
