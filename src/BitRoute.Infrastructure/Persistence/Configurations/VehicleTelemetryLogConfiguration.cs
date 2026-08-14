using BitRoute.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BitRoute.Infrastructure.Persistence.Configurations;

public sealed class VehicleTelemetryLogConfiguration : IEntityTypeConfiguration<VehicleTelemetryLog>
{
    public void Configure(EntityTypeBuilder<VehicleTelemetryLog> builder)
    {
        builder.ToTable("vehicle_telemetry_logs");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.ScheduleId)
            .IsRequired();

        builder.Property(t => t.Latitude)
            .IsRequired();

        builder.Property(t => t.Longitude)
            .IsRequired();

        builder.Property(t => t.CurrentLegIndex)
            .IsRequired();

        builder.Property(t => t.TimestampUtc)
            .IsRequired();

        builder.HasIndex(t => new { t.ScheduleId, t.TimestampUtc });
    }
}
