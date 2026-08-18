using Jobs.Domain.Common;
using Jobs.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobs.Infrastructure.Persistence.Configurations;

internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id)
            .HasConversion(id => id.Value, v => new JobId(v))
            .ValueGeneratedNever();

        builder.Property(j => j.OrganizationId)
            .HasConversion(id => id.Value, v => new OrganizationId(v))
            .IsRequired();

        builder.Property(j => j.Title).HasMaxLength(200).IsRequired();
        builder.Property(j => j.Description).HasMaxLength(4000).IsRequired();

        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.OwnsOne(j => j.Address, a =>
        {
            a.Property(x => x.Street).HasColumnName("address_street").HasMaxLength(200).IsRequired();
            a.Property(x => x.City).HasColumnName("address_city").HasMaxLength(120).IsRequired();
            a.Property(x => x.State).HasColumnName("address_state").HasMaxLength(60).IsRequired();
            a.Property(x => x.ZipCode).HasColumnName("address_zip_code").HasMaxLength(10).IsRequired();
            a.Property(x => x.Latitude).HasColumnName("address_latitude").HasPrecision(9, 6);
            a.Property(x => x.Longitude).HasColumnName("address_longitude").HasPrecision(9, 6);
        });

        builder.Property(j => j.ScheduledDate);
        builder.Property(j => j.StartedAt);
        builder.Property(j => j.CompletedAt);
        builder.Property(j => j.CancelledAt);
        builder.Property(j => j.CancellationReason).HasMaxLength(500);
        builder.Property(j => j.SignatureUrl).HasMaxLength(1000);

        builder.Property(j => j.AssigneeId)
            .HasConversion(id => id!.Value.Value, v => new AssigneeId(v));

        builder.Property(j => j.CustomerId)
            .HasConversion(id => id.Value, v => new CustomerId(v))
            .IsRequired();

        builder.Property(j => j.CreatedAt).IsRequired();
        builder.Property(j => j.UpdatedAt).IsRequired();

        builder.Property(j => j.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasMany(j => j.Photos)
            .WithOne()
            .HasForeignKey(p => p.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(j => j.Photos)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_photos");

        builder.HasIndex(j => new { j.OrganizationId, j.Status, j.ScheduledDate })
            .HasDatabaseName("ix_jobs_org_status_scheduled");

        builder.HasIndex(j => new { j.OrganizationId, j.CreatedAt, j.Id })
            .HasDatabaseName("ix_jobs_org_created_id");

        builder.HasIndex(j => new { j.OrganizationId, j.CustomerId })
            .HasDatabaseName("ix_jobs_org_customer");
    }
}
