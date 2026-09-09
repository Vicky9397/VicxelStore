using Dpm.Payments.Domain;

namespace Dpm.Payments.Application.Abstractions;

public interface IPaymentRepository
{
    Task<Payment?> FindByPublicIdAsync(Guid publicId, CancellationToken ct);

    Task<Payment?> FindByProviderIntentAsync(string provider, string providerIntentId, CancellationToken ct);

    Task<Payment?> FindByOrderIdAsync(long orderId, CancellationToken ct);

    void Add(Payment payment);
}

public interface IPaymentsUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>
/// Exactly-once webhook processing (spec 03 section 3.2). A provider event id is
/// recorded on first receipt; a redelivery finds it already present and is
/// applied at most once.
/// </summary>
public interface IWebhookInbox
{
    /// <summary>
    /// Records the event if it is new. Returns false when this provider event id
    /// has been seen before, which is the signal to skip processing.
    /// </summary>
    Task<bool> TryRecordAsync(
        string providerEventId,
        string provider,
        string type,
        string payloadJson,
        CancellationToken ct);

    Task MarkProcessedAsync(string providerEventId, CancellationToken ct);
}

/// <summary>
/// Replay store for mutating payment calls, keyed by (key hash, endpoint) and
/// retained 24 hours (spec 03 section 3.2). A repeat of the same key returns the
/// first response instead of charging again.
/// </summary>
public interface IIdempotencyStore
{
    Task<IdempotentResponse?> FindAsync(string keyHash, string endpoint, CancellationToken ct);

    Task SaveAsync(string keyHash, string endpoint, int statusCode, string responseJson, CancellationToken ct);
}

public sealed record IdempotentResponse(int StatusCode, string ResponseJson);
