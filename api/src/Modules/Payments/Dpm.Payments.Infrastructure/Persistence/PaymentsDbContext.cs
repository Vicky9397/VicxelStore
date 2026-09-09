using System.Text.Json;
using Dpm.BuildingBlocks.Domain;
using Dpm.BuildingBlocks.Infrastructure;
using Dpm.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dpm.Payments.Infrastructure.Persistence;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<WebhookInboxEntry> WebhookInbox => Set<WebhookInboxEntry>();

    public DbSet<IdempotencyEntry> Idempotency => Set<IdempotencyEntry>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);

    /// <summary>
    /// Writes raised events to the outbox in the same transaction as the payment
    /// change. This is what guarantees a capture is never recorded without its
    /// ledger post eventually following (spec 03 section 3.6).
    /// </summary>
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

public sealed class WebhookInboxEntry
{
    public required string ProviderEventId { get; init; }

    public required string Provider { get; init; }

    public required string Type { get; init; }

    public required string PayloadJson { get; init; }

    public DateTime? ProcessedAtUtc { get; set; }

    public DateTime ReceivedAtUtc { get; init; }
}

public sealed class IdempotencyEntry
{
    public required string KeyHash { get; init; }

    public required string Endpoint { get; init; }

    public required string ResponseJson { get; init; }

    public required int StatusCode { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
