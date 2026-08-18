using JobTracker.SharedKernel.Primitives;

namespace Jobs.Infrastructure.Persistence.Outbox;

internal interface IIntegrationEventMapper
{
    IIntegrationEvent? Map(IDomainEvent domainEvent);
}
