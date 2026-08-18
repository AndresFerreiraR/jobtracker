using Jobs.Domain.Common;
using Jobs.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobs.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new EmployeeId(v))
            .ValueGeneratedNever();

        builder.Property(e => e.OrganizationId)
            .HasConversion(id => id.Value, v => new OrganizationId(v))
            .IsRequired();

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.NameNormalized).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(200);
        builder.Property(e => e.Phone).HasMaxLength(40);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        builder.HasIndex(e => new { e.OrganizationId, e.NameNormalized })
            .IsUnique()
            .HasDatabaseName("ux_employees_org_name");

        builder.HasIndex(e => new { e.OrganizationId, e.CreatedAt })
            .HasDatabaseName("ix_employees_org_created");
    }
}
