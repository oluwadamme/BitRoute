using BitRoute.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BitRoute.Infrastructure.Persistence.Configurations;

public sealed class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.ToTable("seats");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Number)
            .IsRequired()
            .HasMaxLength(50);

        // Seat numbers must be unique per vehicle
        builder.HasIndex(s => new { s.VehicleId, s.Number })
            .IsUnique();
    }
}
