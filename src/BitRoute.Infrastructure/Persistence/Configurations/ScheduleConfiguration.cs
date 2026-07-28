using BitRoute.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BitRoute.Infrastructure.Persistence.Configurations;

public sealed class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.ToTable("schedules");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.DepartureTimeOfDay)
            .IsRequired();

        builder.HasOne(s => s.Route)
            .WithMany()
            .HasForeignKey(s => s.RouteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Vehicle)
            .WithMany()
            .HasForeignKey(s => s.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.ScheduleLegs)
            .WithOne(l => l.Schedule)
            .HasForeignKey(l => l.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
