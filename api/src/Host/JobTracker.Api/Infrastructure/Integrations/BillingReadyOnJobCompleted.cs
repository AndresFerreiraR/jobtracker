using Jobs.IntegrationEvents;
using MediatR;

namespace JobTracker.Api.Infrastructure.Integrations;

/// <summary>
/// Stub billing consumer. Would enqueue an invoice-generation command in a
/// billing bounded context; for now it logs enough to prove the outbox path
/// end-to-end.
/// </summary>
internal sealed class BillingReadyOnJobCompleted(
    ILogger<BillingReadyOnJobCompleted> logger) : INotificationHandler<JobCompletedIntegrationEvent>
{
    public Task Handle(JobCompletedIntegrationEvent evt, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[billing] Ready to bill job {JobId} for customer {CustomerId} in org {OrganizationId} (completed at {CompletedAt}).",
            evt.JobId, evt.CustomerId, evt.OrganizationId, evt.CompletedAt);
        return Task.CompletedTask;
    }
}
