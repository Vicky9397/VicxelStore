using Dpm.BuildingBlocks.Domain;

namespace Dpm.UnitTests;

public sealed class TestClock(DateTime? start = null) : IClock
{
    public DateTime UtcNow { get; private set; } = start ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
