namespace JobTracker.BuildingBlocks.Application.Abstractions;

public sealed record IdempotentReplay(
    int StatusCode,
    string? ResponseBody,
    string? Location);

public interface IIdempotencyStore
{
    Task<IdempotentReplay?> TryGetAsync(
        Guid organizationId,
        string key,
        string method,
        string path,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        Guid organizationId,
        string key,
        string method,
        string path,
        int statusCode,
        string? responseBody,
        string? location,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}
