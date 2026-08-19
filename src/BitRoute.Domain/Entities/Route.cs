namespace BitRoute.Domain.Entities;

public sealed class Route
{
    private readonly List<Stop> _stops = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public IReadOnlyCollection<Stop> Stops => _stops.AsReadOnly();

    private Route() { }

    public static Route Create(string name, IEnumerable<string> stopNames)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Route name cannot be empty.", nameof(name));
        
        var list = stopNames?.ToList();
        if (list == null || list.Count < 2)
            throw new ArgumentException("A route must have at least two stops.", nameof(stopNames));

        var route = new Route
        {
            Id = Guid.NewGuid(),
            Name = name.Trim()
        };

        for (int i = 0; i < list.Count; i++)
        {
            var stop = Stop.Create(list[i], i);
            route._stops.Add(stop);
        }

        return route;
    }

    public void AddStop(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Stop name cannot be empty.", nameof(name));

        var index = _stops.Count;
        var stop = Stop.Create(name, index);
        _stops.Add(stop);
    }
}
