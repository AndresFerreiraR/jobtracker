using Jobs.IntegrationEvents;
using MediatR;

namespace JobTracker.Api.Infrastructure.Integrations;

/// <summary>
/// Stub notifications consumer. Would push an email/SMS through a notifications
/// bounded context; for now it logs to prove the outbox delivery path.
/// </summary>
internal sealed class CustomerNotificationOnJobScheduled(
    ILogger<CustomerNotificationOnJobScheduled> logger)
    : INotificationHandler<JobScheduledIntegrationEvent>,
      INotificationHandler<JobCancelledIntegrationEvent>
{
    public Task Handle(JobScheduledIntegrationEvent evt, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[notifications] Notifying customer that job {JobId} is scheduled for {ScheduledDate} (assignee {AssigneeId}).",
            evt.JobId, evt.ScheduledDate, evt.AssigneeId);
        return Task.CompletedTask;
    }

    public Task Handle(JobCancelledIntegrationEvent evt, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[notifications] Notifying customer that job {JobId} was cancelled. Reason: {Reason}.",
            evt.JobId, evt.Reason);
        return Task.CompletedTask;
    }
}
