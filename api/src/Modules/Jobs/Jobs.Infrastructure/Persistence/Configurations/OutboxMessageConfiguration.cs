using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobs.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).UseIdentityByDefaultColumn();

        builder.Property(o => o.EventId).IsRequired();
        builder.HasIndex(o => o.EventId).IsUnique();

        builder.Property(o => o.Type).HasMaxLength(500).IsRequired();
        builder.Property(o => o.Content).HasColumnType("jsonb").IsRequired();
        builder.Property(o => o.OccurredOn).IsRequired();
        builder.Property(o => o.ProcessedOn);
        builder.Property(o => o.Attempts).IsRequired();
        builder.Property(o => o.LastError);
        builder.Property(o => o.OrganizationId).IsRequired();

        builder.HasIndex(o => o.Id)
            .HasDatabaseName("ix_outbox_unprocessed")
            .HasFilter("processed_on IS NULL");
    }
}
