using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobs.Infrastructure.Persistence.Outbox;

internal sealed class OutboxProcessor(
    OutboxDispatcher dispatcher,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger)
    : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Outbox processor is disabled.");
            return;
        }

        var interval = TimeSpan.FromSeconds(_options.PollingIntervalSeconds);
        logger.LogInformation(
            "Outbox processor started. Polling every {Interval}s, batch {BatchSize}, max attempts {MaxAttempts}.",
            _options.PollingIntervalSeconds, _options.BatchSize, _options.MaxAttempts);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await dispatcher.DispatchBatchAsync(stoppingToken);
                if (processed > 0)
                    logger.LogDebug("Outbox drained {Count} messages", processed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatch loop failed. Sleeping before retry.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Outbox processor stopped.");
    }
}
