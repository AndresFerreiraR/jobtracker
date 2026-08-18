using Jobs.Domain.Common;
using Jobs.Domain.Jobs;
using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Application.Messaging;
using JobTracker.SharedKernel.Results;

namespace Jobs.Application.Jobs.Commands.StartJob;

public sealed record StartJobCommand(Guid JobId) : ICommand;

internal sealed class StartJobCommandHandler(
    IJobRepository repository,
    IDateTimeProvider clock)
    : ICommandHandler<StartJobCommand>
{
    public async Task<Result> Handle(StartJobCommand command, CancellationToken cancellationToken)
    {
        var jobId = new JobId(command.JobId);
        var job = await repository.GetByIdAsync(jobId, cancellationToken);
        if (job is null) return Result.Failure(JobErrors.NotFound(jobId));

        return job.Start(clock.UtcNow);
    }
}
