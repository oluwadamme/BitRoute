using BitRoute.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BitRoute.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Name)
            .IsRequired()
            .HasMaxLength(256);

        // Layout descriptor: the shape of the cabin a seat plan is drawn on.
        builder.Property(v => v.RowCount)
            .IsRequired();

        builder.Property(v => v.SeatsPerRow)
            .IsRequired();

        builder.Property(v => v.AisleAfterColumn);

        // Layout is a read-only view over the three columns above, not a column of its own.
        builder.Ignore(v => v.Layout);

        builder.HasMany(v => v.Seats)
            .WithOne(s => s.Vehicle)
            .HasForeignKey(s => s.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
