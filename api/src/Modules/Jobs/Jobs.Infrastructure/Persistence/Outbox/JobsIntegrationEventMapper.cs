using Jobs.Domain.Jobs.Events;
using Jobs.IntegrationEvents;
using JobTracker.SharedKernel.Primitives;

namespace Jobs.Infrastructure.Persistence.Outbox;

internal sealed class JobsIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent) => domainEvent switch
    {
        JobCreatedDomainEvent e => new JobCreatedIntegrationEvent(
            EventId: Guid.NewGuid(),
            JobId: e.JobId.Value,
            OrganizationId: e.OrganizationId.Value,
            CustomerId: e.CustomerId.Value,
            OccurredOn: e.OccurredOn),

        JobScheduledDomainEvent e => new JobScheduledIntegrationEvent(
            EventId: Guid.NewGuid(),
            JobId: e.JobId.Value,
            OrganizationId: e.OrganizationId.Value,
            AssigneeId: e.AssigneeId.Value,
            ScheduledDate: e.ScheduledDate,
            OccurredOn: e.OccurredOn),

        JobCompletedDomainEvent e => new JobCompletedIntegrationEvent(
            EventId: Guid.NewGuid(),
            JobId: e.JobId.Value,
            OrganizationId: e.OrganizationId.Value,
            CustomerId: e.CustomerId.Value,
            AssigneeId: e.AssigneeId.Value,
            StartedAt: e.StartedAt,
            CompletedAt: e.CompletedAt,
            SignatureUrl: e.SignatureUrl,
            OccurredOn: e.OccurredOn),

        JobCancelledDomainEvent e => new JobCancelledIntegrationEvent(
            EventId: Guid.NewGuid(),
            JobId: e.JobId.Value,
            OrganizationId: e.OrganizationId.Value,
            Reason: e.Reason,
            OccurredOn: e.OccurredOn),

        _ => null,
    };
}
