using BitRoute.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BitRoute.Infrastructure.Persistence.Configurations;

public sealed class SeatBookingConfiguration : IEntityTypeConfiguration<SeatBooking>
{
    public void Configure(EntityTypeBuilder<SeatBooking> builder)
    {
        builder.ToTable("seat_bookings");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.PassengerId)
            .IsRequired();

        builder.Property(b => b.ScheduleId)
            .IsRequired();

        builder.Property(b => b.TravelDate)
            .IsRequired();

        builder.Property(b => b.SeatId)
            .IsRequired();

        builder.Property(b => b.BoardingIndex)
            .IsRequired();

        builder.Property(b => b.AlightingIndex)
            .IsRequired();

        builder.Property(b => b.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(b => b.Price)
            .IsRequired();

        builder.Property(b => b.HoldExpiry)
            .IsRequired(false);

        builder.Property(b => b.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(128);

        // IdempotencyKey must be unique globally to prevent duplicate booking operations
        builder.HasIndex(b => b.IdempotencyKey)
            .IsUnique();

        // Index for fast availability lookups
        builder.HasIndex(b => new { b.ScheduleId, b.TravelDate, b.SeatId, b.Status });

        builder.HasOne(b => b.Schedule)
            .WithMany()
            .HasForeignKey(b => b.ScheduleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Seat)
            .WithMany()
            .HasForeignKey(b => b.SeatId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
