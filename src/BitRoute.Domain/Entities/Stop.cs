namespace BitRoute.Domain.Entities;

public sealed class Stop
{
    public Guid Id { get; private set; }
    public Guid RouteId { get; private set; }
    public string Name { get; private set; } = null!;
    public int Index { get; private set; }

    // Navigation property
    public Route Route { get; private set; } = null!;

    private Stop() { }

    public static Stop Create(string name, int index)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Stop name cannot be empty.", nameof(name));
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Stop index cannot be negative.");

        return new Stop
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Index = index
        };
    }
}
