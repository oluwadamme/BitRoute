using BitRoute.Domain.ValueObjects;

namespace BitRoute.Domain.Entities;

/// <summary>
/// A vehicle and the grid its seats live on. The layout descriptor
/// (<see cref="RowCount"/>, <see cref="SeatsPerRow"/>, <see cref="AisleAfterColumn"/>) gives a
/// client the shape of the cabin, so it can draw the empty aisle column and the gaps in a row
/// without inferring anything from seat labels.
/// </summary>
public sealed class Vehicle
{
    private readonly List<Seat> _seats = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;

    /// <summary>Number of rows, counting from the front of the vehicle.</summary>
    public int RowCount { get; private set; }

    /// <summary>
    /// Total width of a row in columns, INCLUDING the column the aisle occupies.
    /// A 2+2 coach is 5; a 2+1 van is 4.
    /// </summary>
    public int SeatsPerRow { get; private set; }

    /// <summary>
    /// The column the aisle sits immediately after, or <c>null</c> when the vehicle has no aisle.
    /// A 2+2 coach with <c>AisleAfterColumn = 2</c> leaves column 3 empty.
    /// </summary>
    public int? AisleAfterColumn { get; private set; }

    public IReadOnlyCollection<Seat> Seats => _seats.AsReadOnly();

    /// <summary>The persisted layout columns read back as a single descriptor.</summary>
    public VehicleLayout Layout => new(RowCount, SeatsPerRow, AisleAfterColumn);

    private Vehicle() { }

    public static Vehicle Create(string name, VehicleLayout layout, IEnumerable<SeatPlacement> seats)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Vehicle name cannot be empty.", nameof(name));

        var placements = seats?.ToList();
        if (placements == null || placements.Count == 0)
            throw new ArgumentException("A vehicle must have at least one seat.", nameof(seats));

        var occupied = new HashSet<(int Row, int Column)>();

        foreach (var placement in placements)
        {
            if (placement.Row > layout.RowCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(seats), placement.Row,
                    $"Seat '{placement.Number}' sits on row {placement.Row}, past the declared {layout.RowCount} rows.");
            }

            if (placement.Column > layout.SeatsPerRow)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(seats), placement.Column,
                    $"Seat '{placement.Number}' sits on column {placement.Column}, past the declared width of {layout.SeatsPerRow}.");
            }

            if (placement.Column == layout.AisleColumn)
            {
                throw new ArgumentException(
                    $"Seat '{placement.Number}' sits on column {placement.Column}, which is the aisle.", nameof(seats));
            }

            if (!occupied.Add((placement.Row, placement.Column)))
            {
                throw new ArgumentException(
                    $"Two seats occupy row {placement.Row}, column {placement.Column}.", nameof(seats));
            }
        }

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            RowCount = layout.RowCount,
            SeatsPerRow = layout.SeatsPerRow,
            AisleAfterColumn = layout.AisleAfterColumn
        };

        foreach (var placement in placements)
        {
            vehicle._seats.Add(Seat.Create(placement.Number, placement.Row, placement.Column));
        }

        return vehicle;
    }
}
