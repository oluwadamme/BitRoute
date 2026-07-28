namespace BitRoute.Domain.Entities;

public sealed class Vehicle
{
    private readonly List<Seat> _seats = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public IReadOnlyCollection<Seat> Seats => _seats.AsReadOnly();

    private Vehicle() { }

    public static Vehicle Create(string name, IEnumerable<string> seatNumbers)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Vehicle name cannot be empty.", nameof(name));
        
        var seatsList = seatNumbers?.ToList();
        if (seatsList == null || seatsList.Count == 0)
            throw new ArgumentException("A vehicle must have at least one seat.", nameof(seatNumbers));

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Name = name.Trim()
        };

        foreach (var seatNum in seatsList)
        {
            var seat = Seat.Create(seatNum);
            vehicle._seats.Add(seat);
        }

        return vehicle;
    }
}
