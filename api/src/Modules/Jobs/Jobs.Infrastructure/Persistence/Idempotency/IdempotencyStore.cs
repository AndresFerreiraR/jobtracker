using JobTracker.BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Jobs.Infrastructure.Persistence.Idempotency;

internal sealed class IdempotencyStore(JobsDbContext db) : IIdempotencyStore
{
    public async Task<IdempotentReplay?> TryGetAsync(
        Guid organizationId,
        string key,
        string method,
        string path,
        CancellationToken cancellationToken = default)
    {
        var record = await db.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.OrganizationId == organizationId && x.Key == key,
                cancellationToken);

        if (record is null) return null;
        if (record.ExpiresAt <= DateTimeOffset.UtcNow) return null;

        if (!string.Equals(record.Method, method, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(record.Path,   path,   StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Idempotency key '{key}' was previously used for {record.Method} {record.Path}, cannot be reused for {method} {path}.");
        }

        return new IdempotentReplay(record.StatusCode, record.ResponseBody, record.Location);
    }

    public async Task SaveAsync(
        Guid organizationId,
        string key,
        string method,
        string path,
        int statusCode,
        string? responseBody,
        string? location,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var record = new IdempotencyRecord
        {
            OrganizationId = organizationId,
            Key = key,
            Method = method,
            Path = path,
            StatusCode = statusCode,
            ResponseBody = responseBody,
            Location = location,
            CreatedAt = now,
            ExpiresAt = now.Add(ttl),
        };

        db.IdempotencyKeys.Add(record);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // duplicate key — another request beat us here. Safe to ignore.
        }
    }
}
