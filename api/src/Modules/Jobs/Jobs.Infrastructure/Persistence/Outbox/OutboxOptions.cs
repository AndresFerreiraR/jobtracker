namespace Jobs.Infrastructure.Persistence.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int PollingIntervalSeconds { get; init; } = 5;
    public int BatchSize { get; init; } = 32;
    public int MaxAttempts { get; init; } = 5;
    public bool Enabled { get; init; } = true;
}
