namespace Dpm.BuildingBlocks.Domain;

public interface IClock
{
    DateTime UtcNow { get; }
}
