namespace Dpm.Catalog.Domain;

/// <summary>
/// Category tree node. CommissionPct is financial policy: it is seeded and
/// changed only by an administrator (spec 11B section 13.5).
/// </summary>
public sealed class Category
{
    public int Id { get; private set; }

    public int? ParentId { get; private set; }

    public string Slug { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public decimal CommissionPct { get; private set; }

    private Category()
    {
    }
}
