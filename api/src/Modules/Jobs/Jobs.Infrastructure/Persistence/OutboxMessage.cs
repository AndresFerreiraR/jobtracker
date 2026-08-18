namespace Jobs.Infrastructure.Persistence;

internal sealed class OutboxMessage
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public string Type { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTimeOffset OccurredOn { get; set; }
    public DateTimeOffset? ProcessedOn { get; set; }
    public short Attempts { get; set; }
    public string? LastError { get; set; }
    public Guid OrganizationId { get; set; }
}
