using Dpm.BuildingBlocks.Domain;
using Dpm.Payments.Application.Abstractions;
using Dpm.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dpm.Payments.Infrastructure.Persistence;

public sealed class PaymentRepository(PaymentsDbContext dbContext) : IPaymentRepository
{
    public Task<Payment?> FindByPublicIdAsync(Guid publicId, CancellationToken ct) =>
        dbContext.Payments.FirstOrDefaultAsync(p => p.PublicId == publicId, ct);

    public Task<Payment?> FindByProviderIntentAsync(string provider, string providerIntentId, CancellationToken ct) =>
        dbContext.Payments.FirstOrDefaultAsync(
            p => p.Provider == provider && p.ProviderIntentId == providerIntentId, ct);

    public Task<Payment?> FindByOrderIdAsync(long orderId, CancellationToken ct) =>
        dbContext.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

    public void Add(Payment payment) => dbContext.Payments.Add(payment);
}

public sealed class PaymentsUnitOfWork(PaymentsDbContext dbContext) : IPaymentsUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}

/// <summary>
/// Exactly-once webhook handling. The provider event id is the primary key, so a
/// redelivery collides and is reported as already seen rather than reprocessed.
/// </summary>
public sealed class WebhookInbox(PaymentsDbContext dbContext, IClock clock) : IWebhookInbox
{
    public async Task<bool> TryRecordAsync(
        string providerEventId,
        string provider,
        string type,
        string payloadJson,
        CancellationToken ct)
    {
        var seen = await dbContext.WebhookInbox
            .AnyAsync(e => e.ProviderEventId == providerEventId, ct);
        if (seen)
        {
            return false;
        }

        dbContext.WebhookInbox.Add(new WebhookInboxEntry
        {
            ProviderEventId = providerEventId,
            Provider = provider,
            Type = type,
            PayloadJson = payloadJson,
            ReceivedAtUtc = clock.UtcNow,
        });

        try
        {
            await dbContext.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // A concurrent delivery of the same event won the insert; treat this
            // one as the duplicate it is.
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task MarkProcessedAsync(string providerEventId, CancellationToken ct)
    {
        var entry = await dbContext.WebhookInbox
            .FirstOrDefaultAsync(e => e.ProviderEventId == providerEventId, ct);
        if (entry is not null)
        {
            entry.ProcessedAtUtc = clock.UtcNow;
        }
    }
}

public sealed class IdempotencyStore(PaymentsDbContext dbContext, IClock clock) : IIdempotencyStore
{
    /// <summary>Records live for 24 hours (spec 03 section 3.2); older ones are ignored.</summary>
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public async Task<IdempotentResponse?> FindAsync(string keyHash, string endpoint, CancellationToken ct)
    {
        var cutoff = clock.UtcNow.Subtract(Retention);
        var entry = await dbContext.Idempotency
            .FirstOrDefaultAsync(
                e => e.KeyHash == keyHash && e.Endpoint == endpoint && e.CreatedAtUtc >= cutoff, ct);
        return entry is null ? null : new IdempotentResponse(entry.StatusCode, entry.ResponseJson);
    }

    public async Task SaveAsync(
        string keyHash,
        string endpoint,
        int statusCode,
        string responseJson,
        CancellationToken ct)
    {
        dbContext.Idempotency.Add(new IdempotencyEntry
        {
            KeyHash = keyHash,
            Endpoint = endpoint,
            StatusCode = statusCode,
            ResponseJson = responseJson,
            CreatedAtUtc = clock.UtcNow,
        });
        await dbContext.SaveChangesAsync(ct);
    }
}
