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

        builder.Property(s => s.Row)
            .IsRequired();

        builder.Property(s => s.Column)
            .IsRequired();

        // Seat numbers must be unique per vehicle
        builder.HasIndex(s => new { s.VehicleId, s.Number })
            .IsUnique();

        // Two seats can never occupy the same square of the seat plan. The validator rejects
        // this at the edge; this index is the database-level backstop.
        builder.HasIndex(s => new { s.VehicleId, s.Row, s.Column })
            .IsUnique();
    }
}
