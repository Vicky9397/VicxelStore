namespace Dpm.Catalog.Domain;

public sealed class Tag
{
    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    private Tag()
    {
    }

    public static Tag Create(string name) => new() { Name = name.Trim().ToLowerInvariant() };
}
