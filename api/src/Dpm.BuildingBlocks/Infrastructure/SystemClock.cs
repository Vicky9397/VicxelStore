using Dpm.BuildingBlocks.Domain;

namespace Dpm.BuildingBlocks.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
