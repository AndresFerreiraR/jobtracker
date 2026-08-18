using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Jobs.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> tooling.
/// EF Tools prefers this over Program.cs when both exist, so we replicate
/// the runtime configuration chain here.
/// </summary>
internal sealed class JobsDbContextFactory : IDesignTimeDbContextFactory<JobsDbContext>
{
    private const string ConnectionName = "JobTracker";

    private const string DevFallback =
        "Host=localhost;Port=5432;Database=jobtracker;Username=jobtracker;Password=jobtracker";

    public JobsDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();

        var options = new DbContextOptionsBuilder<JobsDbContext>()
            .UseNpgsql(connectionString, npg =>
            {
                npg.MigrationsHistoryTable("__ef_migrations_history", schema: JobsDbContext.SchemaName);
                npg.MigrationsAssembly(typeof(JobsDbContext).Assembly.FullName);
            })
            .UseSnakeCaseNamingConvention()
            .Options;

        return new JobsDbContext(options);
    }

    private static string ResolveConnectionString()
    {
        // 1. Standard .NET double-underscore env override
        var fromStandardEnv = Environment.GetEnvironmentVariable($"ConnectionStrings__{ConnectionName}");
        if (!string.IsNullOrWhiteSpace(fromStandardEnv)) return fromStandardEnv;

        // 2. Legacy custom env override
        var fromLegacyEnv = Environment.GetEnvironmentVariable("JOBTRACKER_CONNECTION");
        if (!string.IsNullOrWhiteSpace(fromLegacyEnv)) return fromLegacyEnv;

        // 3. Host appsettings.json (walks up until it finds Host/JobTracker.Api)
        var fromHostConfig = TryReadHostAppSettings();
        if (!string.IsNullOrWhiteSpace(fromHostConfig)) return fromHostConfig!;

        // 4. Development fallback
        return DevFallback;
    }

    private static string? TryReadHostAppSettings()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var hostDir = Path.Combine(current.FullName, "src", "Host", "JobTracker.Api");
            if (Directory.Exists(hostDir))
            {
                var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
                foreach (var name in new[] { $"appsettings.{env}.json", "appsettings.json" })
                {
                    var path = Path.Combine(hostDir, name);
                    var value = ReadConnectionFromFile(path, ConnectionName);
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
            }
            current = current.Parent;
        }
        return null;
    }

    private static string? ReadConnectionFromFile(string path, string name)
    {
        if (!File.Exists(path)) return null;
        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs)
                && cs.TryGetProperty(name, out var v)
                && v.ValueKind == JsonValueKind.String)
            {
                return v.GetString();
            }
        }
        catch (JsonException)
        {
            // Silently ignore malformed config files at design time.
        }
        return null;
    }
}
