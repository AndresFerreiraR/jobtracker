using Jobs.Domain.Common;
using Jobs.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobs.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, v => new CustomerId(v))
            .ValueGeneratedNever();

        builder.Property(c => c.OrganizationId)
            .HasConversion(id => id.Value, v => new OrganizationId(v))
            .IsRequired();

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.NameNormalized).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(200);
        builder.Property(c => c.Phone).HasMaxLength(40);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        builder.HasIndex(c => new { c.OrganizationId, c.NameNormalized })
            .IsUnique()
            .HasDatabaseName("ux_customers_org_name");

        builder.HasIndex(c => new { c.OrganizationId, c.CreatedAt })
            .HasDatabaseName("ix_customers_org_created");
    }
}
