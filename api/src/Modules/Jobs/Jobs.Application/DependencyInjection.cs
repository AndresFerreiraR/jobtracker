using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Jobs.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddJobsApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining(typeof(AssemblyMarker)));

        services.AddValidatorsFromAssemblyContaining(typeof(AssemblyMarker), includeInternalTypes: true);

        return services;
    }
}
