using BitRoute.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BitRoute.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Type)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(o => o.Content)
            .IsRequired();

        builder.Property(o => o.RetryCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(o => o.NextAttemptUtc);

        builder.HasIndex(o => new { o.ProcessedOnUtc, o.NextAttemptUtc, o.OccurredOnUtc });
    }
}
