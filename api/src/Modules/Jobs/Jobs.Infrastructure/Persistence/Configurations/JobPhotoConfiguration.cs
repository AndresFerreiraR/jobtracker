using Jobs.Domain.Common;
using Jobs.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobs.Infrastructure.Persistence.Configurations;

internal sealed class JobPhotoConfiguration : IEntityTypeConfiguration<JobPhoto>
{
    public void Configure(EntityTypeBuilder<JobPhoto> builder)
    {
        builder.ToTable("job_photos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, v => new JobPhotoId(v))
            .ValueGeneratedNever();

        builder.Property(p => p.JobId)
            .HasConversion(id => id.Value, v => new JobId(v))
            .IsRequired();

        builder.Property(p => p.Url).HasMaxLength(1000).IsRequired();
        builder.Property(p => p.CapturedAt).IsRequired();
        builder.Property(p => p.Caption).HasMaxLength(500);

        builder.HasIndex(p => p.JobId).HasDatabaseName("ix_job_photos_job_id");
    }
}
