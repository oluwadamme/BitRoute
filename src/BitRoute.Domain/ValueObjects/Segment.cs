namespace BitRoute.Domain.ValueObjects;

/// <summary>
/// The half-open interval of stop indices a booking occupies, written [Start, End).
/// A trip from stop index 0 to stop index 2 occupies [0, 2). The interval is half-open,
/// so a trip ending at index 2 and a trip starting at index 2 do not conflict.
/// </summary>
public readonly record struct Segment
{
    public int Start { get; }
    public int End { get; }

    private Segment(int start, int end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Creates a validated segment. The boarding index must be non-negative and the
    /// alighting index must be strictly greater, so a zero-length or inverted journey
    /// can never be constructed.
    /// </summary>
    public static Segment Create(int start, int end)
    {
        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "Boarding index cannot be negative.");
        }

        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), end, "Alighting index must be greater than the boarding index.");
        }

        return new Segment(start, end);
    }

    /// <summary>
    /// Two segments conflict when ExistingStart &lt; RequestedEnd AND ExistingEnd &gt; RequestedStart.
    /// Touching at a boundary (this.End == other.Start, or vice versa) does not overlap,
    /// because the interval is half-open. This is the single source of truth for the rule.
    /// </summary>
    public bool Overlaps(Segment other) => Start < other.End && End > other.Start;
}
