using Jobs.Domain.Customers;
using Jobs.Domain.Employees;
using Jobs.Domain.Jobs;
using Jobs.Infrastructure.Persistence.Idempotency;
using JobTracker.BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Jobs.Infrastructure.Persistence;

public sealed class JobsDbContext(DbContextOptions<JobsDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string SchemaName = "jobs";

    internal DbSet<Job> Jobs => Set<Job>();
    internal DbSet<Customer> Customers => Set<Customer>();
    internal DbSet<Employee> Employees => Set<Employee>();
    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    internal DbSet<IdempotencyRecord> IdempotencyKeys => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobsDbContext).Assembly);
    }
}
