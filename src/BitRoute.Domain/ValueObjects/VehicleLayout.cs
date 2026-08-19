namespace BitRoute.Domain.ValueObjects;

/// <summary>
/// The shape of a vehicle's cabin — the grid its seats are placed on.
/// <para>
/// <c>SeatsPerRow</c> is the TOTAL width of a row in columns, including the column the aisle
/// occupies: a 2+2 coach is 5, a 2+1 van is 4. <c>AisleAfterColumn</c> is the column the aisle
/// sits immediately after (so the aisle itself is column <c>AisleAfterColumn + 1</c>), or
/// <c>null</c> when the vehicle has no aisle.
/// </para>
/// </summary>
public readonly record struct VehicleLayout
{
    public int RowCount { get; }
    public int SeatsPerRow { get; }
    public int? AisleAfterColumn { get; }

    public VehicleLayout(int rowCount, int seatsPerRow, int? aisleAfterColumn)
    {
        if (rowCount < 1)
            throw new ArgumentOutOfRangeException(nameof(rowCount), rowCount, "A vehicle must have at least one row.");

        if (seatsPerRow < 1)
            throw new ArgumentOutOfRangeException(nameof(seatsPerRow), seatsPerRow, "A vehicle must have at least one column.");

        // The aisle occupies column (aisleAfterColumn + 1), so it must leave at least one
        // column on either side of it.
        if (aisleAfterColumn is { } aisle && (aisle < 1 || aisle >= seatsPerRow))
        {
            throw new ArgumentOutOfRangeException(
                nameof(aisleAfterColumn), aisleAfterColumn,
                $"The aisle must fall between column 1 and column {seatsPerRow - 1}.");
        }

        RowCount = rowCount;
        SeatsPerRow = seatsPerRow;
        AisleAfterColumn = aisleAfterColumn;
    }

    /// <summary>The column the aisle occupies, or <c>null</c> when the vehicle has no aisle.</summary>
    public int? AisleColumn => AisleAfterColumn + 1;
}
