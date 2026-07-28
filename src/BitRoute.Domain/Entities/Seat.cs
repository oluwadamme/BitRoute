namespace BitRoute.Domain.Entities;

public sealed class Seat
{
    public Guid Id { get; private set; }
    public Guid VehicleId { get; private set; }
    public string Number { get; private set; } = null!;

    // Navigation property
    public Vehicle Vehicle { get; private set; } = null!;

    private Seat() { }

    public static Seat Create(string number)
    {
        if (string.IsNullOrWhiteSpace(number))
            throw new ArgumentException("Seat number cannot be empty.", nameof(number));

        return new Seat
        {
            Id = Guid.NewGuid(),
            Number = number.Trim().ToUpperInvariant()
        };
    }
}
