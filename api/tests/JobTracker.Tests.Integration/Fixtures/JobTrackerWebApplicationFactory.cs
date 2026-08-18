using Jobs.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.Tests.Integration.Fixtures;

public sealed class JobTrackerWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:JobTracker"] = connectionString,
            });
        });

        builder.ConfigureServices(services =>
        {
            using var scope = services
                .BuildServiceProvider()
                .CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
            db.Database.Migrate();
        });
    }
}
