using Dpm.BuildingBlocks.Domain;

namespace Dpm.Catalog.Domain;

public sealed class ProductVersion : Entity
{
    public long ProductId { get; private set; }

    public string VersionNumber { get; private set; } = string.Empty;

    public string? Changelog { get; private set; }

    public DateTime ReleasedAtUtc { get; private set; }

    private ProductVersion()
    {
    }

    internal static ProductVersion Create(string versionNumber, string? changelog, IClock clock) => new()
    {
        VersionNumber = Guard.AgainstNullOrWhiteSpace(versionNumber, nameof(versionNumber)).Trim(),
        Changelog = changelog?.Trim(),
        ReleasedAtUtc = clock.UtcNow,
    };
}
