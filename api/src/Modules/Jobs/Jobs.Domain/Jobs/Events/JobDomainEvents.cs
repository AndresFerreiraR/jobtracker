using Jobs.Domain.Common;
using JobTracker.SharedKernel.Primitives;

namespace Jobs.Domain.Jobs.Events;

public sealed record JobCreatedDomainEvent(
    JobId JobId,
    OrganizationId OrganizationId,
    CustomerId CustomerId,
    DateTimeOffset OccurredOn) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed record JobScheduledDomainEvent(
    JobId JobId,
    OrganizationId OrganizationId,
    AssigneeId AssigneeId,
    DateTimeOffset ScheduledDate,
    DateTimeOffset OccurredOn) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed record JobStartedDomainEvent(
    JobId JobId,
    OrganizationId OrganizationId,
    DateTimeOffset StartedAt,
    DateTimeOffset OccurredOn) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed record JobCompletedDomainEvent(
    JobId JobId,
    OrganizationId OrganizationId,
    CustomerId CustomerId,
    AssigneeId AssigneeId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string SignatureUrl,
    DateTimeOffset OccurredOn) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed record JobCancelledDomainEvent(
    JobId JobId,
    OrganizationId OrganizationId,
    string Reason,
    DateTimeOffset OccurredOn) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
}
