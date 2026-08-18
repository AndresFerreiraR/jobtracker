using System.Text.Json;
using JobTracker.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Jobs.Infrastructure.Persistence.Outbox;

internal sealed class InsertOutboxMessagesInterceptor(IIntegrationEventMapper mapper) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context as JobsDbContext;
        if (context is null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        var aggregates = context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(e => e.Entity.Events.Count > 0)
            .Select(e => e.Entity)
            .ToArray();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DrainEvents())
            {
                var integrationEvent = mapper.Map(domainEvent);
                if (integrationEvent is null) continue;

                context.OutboxMessages.Add(new OutboxMessage
                {
                    EventId = integrationEvent.EventId,
                    Type = integrationEvent.GetType().FullName!,
                    Content = JsonSerializer.Serialize((object)integrationEvent, JsonOptions),
                    OccurredOn = integrationEvent.OccurredOn,
                    Attempts = 0,
                    OrganizationId = ExtractOrganizationId(integrationEvent),
                });
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static Guid ExtractOrganizationId(IIntegrationEvent evt)
    {
        var prop = evt.GetType().GetProperty("OrganizationId");
        return prop?.GetValue(evt) is Guid g ? g : Guid.Empty;
    }
}
