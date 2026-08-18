using JobTracker.SharedKernel.Primitives;

namespace JobTracker.BuildingBlocks.Application.Abstractions;

public interface IOutboxWriter
{
    Task EnqueueAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : IIntegrationEvent;
}
