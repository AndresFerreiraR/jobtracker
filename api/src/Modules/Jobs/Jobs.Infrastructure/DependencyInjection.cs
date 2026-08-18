using Jobs.Application.Customers.Queries;
using Jobs.Application.Employees.Queries;
using Jobs.Application.Jobs.Queries.GetJobById;
using Jobs.Domain.Customers;
using Jobs.Domain.Employees;
using Jobs.Domain.Jobs;
using Jobs.Infrastructure.Persistence;
using Jobs.Infrastructure.Persistence.Idempotency;
using Jobs.Infrastructure.Persistence.Outbox;
using Jobs.Infrastructure.Persistence.Queries;
using Jobs.Infrastructure.Persistence.Repositories;
using JobTracker.BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jobs.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddJobsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("JobTracker")
            ?? throw new InvalidOperationException("Missing connection string 'JobTracker'.");

        services.AddSingleton<IIntegrationEventMapper, JobsIntegrationEventMapper>();
        services.AddSingleton<InsertOutboxMessagesInterceptor>();

        services.AddDbContext<JobsDbContext>((sp, opt) =>
        {
            opt.UseNpgsql(connectionString, npg =>
            {
                npg.MigrationsHistoryTable("__ef_migrations_history", schema: JobsDbContext.SchemaName);
                npg.MigrationsAssembly(typeof(JobsDbContext).Assembly.FullName);
            });
            opt.UseSnakeCaseNamingConvention();
            opt.AddInterceptors(sp.GetRequiredService<InsertOutboxMessagesInterceptor>());
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<JobsDbContext>());
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IJobQueryService, JobQueryService>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerQueryService, CustomerQueryService>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IEmployeeQueryService, EmployeeQueryService>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();

        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName));
        services.AddSingleton<OutboxDispatcher>();
        services.AddHostedService<OutboxProcessor>();

        return services;
    }
}
