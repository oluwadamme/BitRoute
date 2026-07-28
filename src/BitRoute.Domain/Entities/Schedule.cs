namespace BitRoute.Domain.Entities;

public sealed class Schedule
{
    private readonly List<ScheduleLeg> _scheduleLegs = new();

    public Guid Id { get; private set; }
    public Guid RouteId { get; private set; }
    public Guid VehicleId { get; private set; }
    public TimeOnly DepartureTimeOfDay { get; private set; }

    // Navigation properties
    public Route Route { get; private set; } = null!;
    public Vehicle Vehicle { get; private set; } = null!;
    public IReadOnlyCollection<ScheduleLeg> ScheduleLegs => _scheduleLegs.AsReadOnly();

    private Schedule() { }

    public static Schedule Create(Guid routeId, Guid vehicleId, TimeOnly departureTimeOfDay, IEnumerable<ScheduleLeg> legs)
    {
        if (routeId == Guid.Empty)
            throw new ArgumentException("RouteId must not be empty.", nameof(routeId));
        if (vehicleId == Guid.Empty)
            throw new ArgumentException("VehicleId must not be empty.", nameof(vehicleId));

        var legList = legs?.ToList();
        if (legList == null || legList.Count == 0)
            throw new ArgumentException("A schedule must have at least one leg defined.", nameof(legs));

        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            RouteId = routeId,
            VehicleId = vehicleId,
            DepartureTimeOfDay = departureTimeOfDay
        };

        foreach (var leg in legList)
        {
            // Verify that the leg is created for this schedule (its scheduleId should match or be set here)
            var scheduleLeg = ScheduleLeg.Create(leg.StartStopIndex, leg.EndStopIndex, leg.Fare);
            scheduleLeg.GetType().GetProperty(nameof(ScheduleLeg.ScheduleId))?.SetValue(scheduleLeg, schedule.Id);
            schedule._scheduleLegs.Add(scheduleLeg);
        }

        return schedule;
    }

    public int GetPrice(int boardingIndex, int alightingIndex)
    {
        if (boardingIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(boardingIndex));
        if (alightingIndex <= boardingIndex)
            throw new ArgumentException("Alighting index must be greater than boarding index.");

        var expectedLegCount = alightingIndex - boardingIndex;
        var matchingLegs = _scheduleLegs
            .Where(l => l.StartStopIndex >= boardingIndex && l.EndStopIndex <= alightingIndex)
            .ToList();

        if (matchingLegs.Count < expectedLegCount)
        {
            throw new InvalidOperationException(
                $"Incomplete schedule configuration. Missing legs between index {boardingIndex} and {alightingIndex}.");
        }

        return matchingLegs.Sum(l => l.Fare);
    }
}
