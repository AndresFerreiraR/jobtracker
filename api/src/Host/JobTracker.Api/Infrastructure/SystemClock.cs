using JobTracker.BuildingBlocks.Application.Abstractions;

namespace JobTracker.Api.Infrastructure;

internal sealed class SystemClock : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
