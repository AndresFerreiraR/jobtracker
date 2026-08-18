namespace JobTracker.Api.Infrastructure.Idempotency;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    public string HeaderName { get; init; } = "Idempotency-Key";
    public int MaxKeyLength { get; init; } = 128;
    public int TtlHours { get; init; } = 24;
    public bool Enabled { get; init; } = true;
}
