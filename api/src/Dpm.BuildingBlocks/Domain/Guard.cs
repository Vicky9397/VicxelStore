namespace Dpm.BuildingBlocks.Domain;

public static class Guard
{
    public static string AgainstNullOrWhiteSpace(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }

        return value;
    }

    public static T AgainstNull<T>(T? value, string name)
        where T : class
    {
        return value ?? throw new ArgumentNullException(name);
    }

    public static string AgainstOverflow(string value, int maxLength, string name)
    {
        if (value.Length > maxLength)
        {
            throw new ArgumentException($"{name} cannot exceed {maxLength} characters.", name);
        }

        return value;
    }
}
