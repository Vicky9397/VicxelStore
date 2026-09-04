namespace Dpm.BuildingBlocks.Infrastructure;

/// <summary>
/// Persisted domain event awaiting dispatch (dbo.OutboxMessages). Written in the
/// same transaction as the aggregate change; a background dispatcher delivers it
/// at-least-once to idempotent handlers.
/// </summary>
public sealed class OutboxMessage
{
    public long Id { get; init; }

    public required Guid EventId { get; init; }

    public required string Type { get; init; }

    public required string PayloadJson { get; init; }

    public required DateTime OccurredAtUtc { get; init; }

    public DateTime? ProcessedAtUtc { get; set; }

    public string? Error { get; set; }
}
