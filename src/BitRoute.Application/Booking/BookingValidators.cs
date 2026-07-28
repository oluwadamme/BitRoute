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
        RuleFor(v => v.Seats).NotNull().Must(s => s != null && s.Count > 0)
            .WithMessage("A vehicle must have at least one seat.");
        RuleForEach(v => v.Seats).NotEmpty().MaximumLength(50);
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
