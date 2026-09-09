namespace Dpm.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int PollSeconds { get; init; } = 5;

    public int BatchSize { get; init; } = 50;

    /// <summary>
    /// Attempts before a message is parked. A parked message stays unprocessed
    /// and visible so a human can act on it; it is never silently dropped,
    /// because a money event that never lands is a reconciliation break.
    /// </summary>
    public int MaxAttempts { get; init; } = 10;
}
