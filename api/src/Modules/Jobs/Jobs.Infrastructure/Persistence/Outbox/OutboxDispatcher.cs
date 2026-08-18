using System.Text.Json;
using JobTracker.SharedKernel.Primitives;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobs.Infrastructure.Persistence.Outbox;

internal sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly OutboxOptions _options = options.Value;

    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var messages = await db.Database
            .SqlQuery<OutboxMessageRow>($"""
                SELECT id, event_id, type, content, occurred_on, attempts
                FROM jobs.outbox_messages
                WHERE processed_on IS NULL AND attempts < {_options.MaxAttempts}
                ORDER BY id
                LIMIT {_options.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            await tx.CommitAsync(cancellationToken);
            return 0;
        }

        var processedIds = new List<long>();
        var failedIds = new List<(long Id, string Error)>();

        foreach (var msg in messages)
        {
            var integrationEvent = TryDeserialize(msg);
            if (integrationEvent is null)
            {
                failedIds.Add((msg.Id, $"Unknown integration event type '{msg.Type}'."));
                continue;
            }

            try
            {
                await publisher.Publish(integrationEvent, cancellationToken);
                processedIds.Add(msg.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish outbox message {MessageId}", msg.Id);
                failedIds.Add((msg.Id, ex.Message));
            }
        }

        if (processedIds.Count > 0)
        {
            var idArray = processedIds.ToArray();
            await db.Database.ExecuteSqlAsync($"""
                UPDATE jobs.outbox_messages
                SET processed_on = NOW() AT TIME ZONE 'UTC', attempts = attempts + 1, last_error = NULL
                WHERE id = ANY({idArray})
                """, cancellationToken);
        }

        foreach (var (id, error) in failedIds)
        {
            var truncated = error.Length > 2000 ? error[..2000] : error;
            await db.Database.ExecuteSqlAsync($"""
                UPDATE jobs.outbox_messages
                SET attempts = attempts + 1, last_error = {truncated}
                WHERE id = {id}
                """, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return messages.Count;
    }

    private IIntegrationEvent? TryDeserialize(OutboxMessageRow msg)
    {
        var type = Type.GetType(msg.Type)
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(msg.Type))
                .FirstOrDefault(t => t is not null);

        if (type is null) return null;

        return JsonSerializer.Deserialize(msg.Content, type, JsonOptions) as IIntegrationEvent;
    }

    private sealed record OutboxMessageRow(
        long Id,
        Guid EventId,
        string Type,
        string Content,
        DateTimeOffset OccurredOn,
        short Attempts);
}
