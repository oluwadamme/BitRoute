using BitRoute.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BitRoute.Infrastructure.Persistence.Configurations;

public sealed class ScheduleLegConfiguration : IEntityTypeConfiguration<ScheduleLeg>
{
    public void Configure(EntityTypeBuilder<ScheduleLeg> builder)
    {
        builder.ToTable("schedule_legs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.StartStopIndex)
            .IsRequired();

        builder.Property(l => l.EndStopIndex)
            .IsRequired();

        builder.Property(l => l.Fare)
            .IsRequired();

        // Index on ScheduleId, StartStopIndex, and EndStopIndex to ensure legs are unique
        builder.HasIndex(l => new { l.ScheduleId, l.StartStopIndex, l.EndStopIndex })
            .IsUnique();
    }
}
