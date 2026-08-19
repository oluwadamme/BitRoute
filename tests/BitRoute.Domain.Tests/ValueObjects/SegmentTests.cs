using BitRoute.Domain.ValueObjects;

namespace BitRoute.Domain.Tests.ValueObjects;

public class SegmentTests
{
    [Theory]
    [InlineData(0, 3, 2, 4)] // [0,3) and [2,4) share index 2
    [InlineData(2, 4, 0, 3)] // reversed arguments, same pair
    [InlineData(1, 5, 4, 6)] // overlap on a single index
    public void Overlaps_ReturnsTrue_WhenIntervalsPartiallyOverlap(int aStart, int aEnd, int bStart, int bEnd)
    {
        var a = Segment.Create(aStart, aEnd);
        var b = Segment.Create(bStart, bEnd);

        Assert.True(a.Overlaps(b));
    }

    [Theory]
    [InlineData(0, 4, 1, 2)] // [1,2) sits fully inside [0,4)
    [InlineData(1, 2, 0, 4)] // and the containing case, reversed
    public void Overlaps_ReturnsTrue_WhenOneIntervalContainsAnother(int aStart, int aEnd, int bStart, int bEnd)
    {
        var a = Segment.Create(aStart, aEnd);
        var b = Segment.Create(bStart, bEnd);

        Assert.True(a.Overlaps(b));
    }

    [Fact]
    public void Overlaps_ReturnsTrue_WhenIntervalsAreIdentical()
    {
        var a = Segment.Create(0, 4);
        var b = Segment.Create(0, 4);

        Assert.True(a.Overlaps(b));
    }

    [Theory]
    [InlineData(0, 2, 2, 4)] // A to C then C to E, the seat-sharing case
    [InlineData(2, 4, 0, 2)] // same pair reversed
    public void Overlaps_ReturnsFalse_WhenIntervalsTouchAtBoundary(int aStart, int aEnd, int bStart, int bEnd)
    {
        var a = Segment.Create(aStart, aEnd);
        var b = Segment.Create(bStart, bEnd);

        Assert.False(a.Overlaps(b));
    }

    [Theory]
    [InlineData(0, 1, 3, 4)]
    [InlineData(3, 4, 0, 1)]
    public void Overlaps_ReturnsFalse_WhenIntervalsAreDisjoint(int aStart, int aEnd, int bStart, int bEnd)
    {
        var a = Segment.Create(aStart, aEnd);
        var b = Segment.Create(bStart, bEnd);

        Assert.False(a.Overlaps(b));
    }

    [Theory]
    [InlineData(0, 3, 2, 4)] // overlapping
    [InlineData(0, 2, 2, 4)] // touching
    [InlineData(0, 1, 3, 4)] // disjoint
    public void Overlaps_IsSymmetric(int aStart, int aEnd, int bStart, int bEnd)
    {
        var a = Segment.Create(aStart, aEnd);
        var b = Segment.Create(bStart, bEnd);

        Assert.Equal(a.Overlaps(b), b.Overlaps(a));
    }

    [Fact]
    public void Create_BuildsSegment_WhenIndicesAreValid()
    {
        var segment = Segment.Create(1, 3);

        Assert.Equal(1, segment.Start);
        Assert.Equal(3, segment.End);
    }

    [Fact]
    public void Create_Throws_WhenEndEqualsStart()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Segment.Create(2, 2));
    }

    [Fact]
    public void Create_Throws_WhenEndLessThanStart()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Segment.Create(3, 1));
    }

    [Fact]
    public void Create_Throws_WhenStartIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Segment.Create(-1, 2));
    }
}
