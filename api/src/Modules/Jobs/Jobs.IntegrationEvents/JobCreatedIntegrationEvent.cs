using JobTracker.SharedKernel.Primitives;

namespace Jobs.IntegrationEvents;

public sealed record JobCreatedIntegrationEvent(
    Guid EventId,
    Guid JobId,
    Guid OrganizationId,
    Guid CustomerId,
    DateTimeOffset OccurredOn) : IIntegrationEvent;

public sealed record JobScheduledIntegrationEvent(
    Guid EventId,
    Guid JobId,
    Guid OrganizationId,
    Guid AssigneeId,
    DateTimeOffset ScheduledDate,
    DateTimeOffset OccurredOn) : IIntegrationEvent;

public sealed record JobCompletedIntegrationEvent(
    Guid EventId,
    Guid JobId,
    Guid OrganizationId,
    Guid CustomerId,
    Guid AssigneeId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string SignatureUrl,
    DateTimeOffset OccurredOn) : IIntegrationEvent;

public sealed record JobCancelledIntegrationEvent(
    Guid EventId,
    Guid JobId,
    Guid OrganizationId,
    string Reason,
    DateTimeOffset OccurredOn) : IIntegrationEvent;
