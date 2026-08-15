namespace BitRoute.Domain.Entities;

/// <summary>
/// A physical seat in a vehicle. <see cref="Number"/> is the human-readable label the passenger
/// sees; <see cref="Row"/> and <see cref="Column"/> are where the seat actually sits, so a seat
/// plan can be drawn from data rather than guessed from the label.
/// </summary>
public sealed class Seat
{
    public Guid Id { get; private set; }
    public Guid VehicleId { get; private set; }
    public string Number { get; private set; } = null!;

    /// <summary>1-based row, counting from the front of the vehicle (row 1 is the front).</summary>
    public int Row { get; private set; }

    /// <summary>
    /// 1-based column, counted left-to-right across the full width of the cabin including the
    /// position the aisle occupies. A 2+2 coach uses columns 1, 2, 4, 5 with column 3 left empty
    /// as the aisle.
    /// </summary>
    public int Column { get; private set; }

    // Navigation property
    public Vehicle Vehicle { get; private set; } = null!;

    private Seat() { }

    public static Seat Create(string number, int row, int column)
    {
        if (string.IsNullOrWhiteSpace(number))
            throw new ArgumentException("Seat number cannot be empty.", nameof(number));

        if (row < 1)
            throw new ArgumentOutOfRangeException(nameof(row), row, "Seat row must be 1-based and positive.");

        if (column < 1)
            throw new ArgumentOutOfRangeException(nameof(column), column, "Seat column must be 1-based and positive.");

        return new Seat
        {
            Id = Guid.NewGuid(),
            Number = number.Trim().ToUpperInvariant(),
            Row = row,
            Column = column
        };
    }
}
