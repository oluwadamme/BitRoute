using FluentValidation;

namespace BitRoute.Application.Booking;

public sealed class CreateRouteRequestValidator : AbstractValidator<CreateRouteRequest>
{
    public CreateRouteRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Stops).NotNull().Must(s => s != null && s.Count >= 2)
            .WithMessage("A route must have at least two stops.");
        RuleForEach(r => r.Stops).NotEmpty().MaximumLength(256);
    }
}

public sealed class CreateVehicleRequestValidator : AbstractValidator<CreateVehicleRequest>
{
    public CreateVehicleRequestValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(256);

        RuleFor(v => v.RowCount).GreaterThan(0);
        RuleFor(v => v.SeatsPerRow).GreaterThan(0);

        // The aisle occupies column (AisleAfterColumn + 1), so it must leave at least one
        // column on either side of it.
        RuleFor(v => v.AisleAfterColumn)
            .Must((request, aisle) => aisle is null || (aisle >= 1 && aisle < request.SeatsPerRow))
            .WithMessage(v => $"AisleAfterColumn must fall between 1 and {v.SeatsPerRow - 1}, or be null for a vehicle with no aisle.");

        RuleFor(v => v.Seats).NotNull().Must(s => s != null && s.Count > 0)
            .WithMessage("A vehicle must have at least one seat.");

        RuleForEach(v => v.Seats).ChildRules(seat =>
        {
            seat.RuleFor(s => s.Number).NotEmpty().MaximumLength(50);
            seat.RuleFor(s => s.Row).GreaterThan(0);
            seat.RuleFor(s => s.Column).GreaterThan(0);
        });

        // Every seat must land inside the declared grid, off the aisle column.
        RuleForEach(v => v.Seats)
            .Must((request, seat) => seat.Row <= request.RowCount)
            .WithMessage((request, seat) => $"Seat '{seat.Number}' sits on row {seat.Row}, past the declared {request.RowCount} rows.")
            .Must((request, seat) => seat.Column <= request.SeatsPerRow)
            .WithMessage((request, seat) => $"Seat '{seat.Number}' sits on column {seat.Column}, past the declared width of {request.SeatsPerRow}.")
            .Must((request, seat) => request.AisleAfterColumn is null || seat.Column != request.AisleAfterColumn + 1)
            .WithMessage((request, seat) => $"Seat '{seat.Number}' sits on column {seat.Column}, which is the aisle.");

        // No two seats may occupy the same square of the plan, and no label may repeat.
        RuleFor(v => v.Seats)
            .Must(seats => seats is null || seats
                .GroupBy(s => (s.Row, s.Column))
                .All(g => g.Count() == 1))
            .WithMessage("Two or more seats occupy the same row and column.")
            .Must(seats => seats is null || seats
                .Where(s => !string.IsNullOrWhiteSpace(s.Number))
                .GroupBy(s => s.Number.Trim(), StringComparer.OrdinalIgnoreCase)
                .All(g => g.Count() == 1))
            .WithMessage("Seat numbers must be unique within a vehicle.");
    }
}

public sealed class CreateScheduleRequestValidator : AbstractValidator<CreateScheduleRequest>
{
    public CreateScheduleRequestValidator()
    {
        RuleFor(s => s.RouteId).NotEmpty();
        RuleFor(s => s.VehicleId).NotEmpty();
        RuleFor(s => s.DepartureTimeOfDay).NotEmpty();
        RuleFor(s => s.Legs).NotNull().Must(l => l != null && l.Count > 0)
            .WithMessage("A schedule must define at least one leg.");
        RuleForEach(s => s.Legs).SetValidator(new CreateScheduleLegValidator());
    }
}

public sealed class CreateScheduleLegValidator : AbstractValidator<CreateScheduleLegDto>
{
    public CreateScheduleLegValidator()
    {
        RuleFor(l => l.StartStopIndex).GreaterThanOrEqualTo(0);
        RuleFor(l => l.EndStopIndex).GreaterThan(l => l.StartStopIndex)
            .WithMessage("End index must be greater than start index.");
        RuleFor(l => l.Fare).GreaterThanOrEqualTo(0);
    }
}

public sealed class HoldSeatRequestValidator : AbstractValidator<HoldSeatRequest>
{
    public HoldSeatRequestValidator()
    {
        RuleFor(b => b.ScheduleId).NotEmpty();
        RuleFor(b => b.TravelDate).NotEmpty()
            .Must(d => d >= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Travel date cannot be in the past.");
        RuleFor(b => b.SeatId).NotEmpty();
        RuleFor(b => b.BoardingIndex).GreaterThanOrEqualTo(0);
        RuleFor(b => b.AlightingIndex).GreaterThan(b => b.BoardingIndex)
            .WithMessage("Alighting index must be greater than boarding index.");
        RuleFor(b => b.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
