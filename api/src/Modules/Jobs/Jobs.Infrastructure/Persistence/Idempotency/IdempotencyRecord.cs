namespace Jobs.Infrastructure.Persistence.Idempotency;

internal sealed class IdempotencyRecord
{
    public Guid OrganizationId { get; set; }
    public string Key { get; set; } = null!;
    public string Method { get; set; } = null!;
    public string Path { get; set; } = null!;
    public int StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? Location { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
