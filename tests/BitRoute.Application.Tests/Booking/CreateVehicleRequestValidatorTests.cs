using BitRoute.Application.Booking;
using Xunit;

namespace BitRoute.Application.Tests.Booking;

/// <summary>
/// The seat plan is only honest if the positions it is drawn from are. These cover the edge:
/// a request whose seats fall outside the declared cabin, land on the aisle, or share a square
/// must never reach the domain.
/// </summary>
public class CreateVehicleRequestValidatorTests
{
    private readonly CreateVehicleRequestValidator _validator = new();

    /// <summary>A well-formed 2+2 coach: two rows of A,B | aisle | C,D.</summary>
    private static CreateVehicleRequest ValidCoach(params CreateSeatDto[] overrideSeats)
        => new(
            "Luxury Coach",
            RowCount: 2,
            SeatsPerRow: 5,
            AisleAfterColumn: 2,
            Seats: overrideSeats.Length > 0
                ? overrideSeats.ToList()
                : new List<CreateSeatDto>
                {
                    new("1A", 1, 1), new("1B", 1, 2), new("1C", 1, 4), new("1D", 1, 5),
                    new("2A", 2, 1), new("2B", 2, 2), new("2C", 2, 4), new("2D", 2, 5)
                });

    [Fact]
    public void ValidCoachLayout_Passes()
    {
        Assert.True(_validator.Validate(ValidCoach()).IsValid);
    }

    [Fact]
    public void VehicleWithNoAisle_Passes()
    {
        var request = new CreateVehicleRequest(
            "Shuttle", 2, 2, null,
            new List<CreateSeatDto> { new("1A", 1, 1), new("1B", 1, 2), new("2A", 2, 1) });

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void SeatPastTheDeclaredRowCount_Fails()
    {
        var result = _validator.Validate(ValidCoach(new CreateSeatDto("3A", 3, 1)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("past the declared 2 rows"));
    }

    [Fact]
    public void SeatPastTheDeclaredWidth_Fails()
    {
        var result = _validator.Validate(ValidCoach(new CreateSeatDto("1E", 1, 6)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("past the declared width of 5"));
    }

    [Fact]
    public void SeatOnTheAisleColumn_Fails()
    {
        var result = _validator.Validate(ValidCoach(new CreateSeatDto("1C", 1, 3)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("which is the aisle"));
    }

    [Fact]
    public void TwoSeatsOnTheSameSquare_Fails()
    {
        var result = _validator.Validate(ValidCoach(
            new CreateSeatDto("1A", 1, 1),
            new CreateSeatDto("1B", 1, 1)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("same row and column"));
    }

    [Fact]
    public void DuplicateSeatNumbers_Fails()
    {
        var result = _validator.Validate(ValidCoach(
            new CreateSeatDto("1A", 1, 1),
            new CreateSeatDto("1a", 1, 2)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Seat numbers must be unique"));
    }

    [Theory]
    [InlineData(0)]  // an aisle before column 1 has nothing to its left
    [InlineData(5)]  // an aisle after the last column has nothing to its right
    public void AisleOutsideTheCabin_Fails(int aisleAfterColumn)
    {
        var request = new CreateVehicleRequest(
            "Odd Coach", 1, 5, aisleAfterColumn,
            new List<CreateSeatDto> { new("1A", 1, 1) });

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("AisleAfterColumn must fall between"));
    }

    [Fact]
    public void NonPositiveRowOrColumn_Fails()
    {
        var result = _validator.Validate(ValidCoach(new CreateSeatDto("1A", 0, 0)));

        Assert.False(result.IsValid);
    }
}
