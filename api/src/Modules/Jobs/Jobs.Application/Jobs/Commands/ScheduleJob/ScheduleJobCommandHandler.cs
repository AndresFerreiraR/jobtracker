using Jobs.Domain.Common;
using Jobs.Domain.Jobs;
using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Application.Messaging;
using JobTracker.SharedKernel.Results;

namespace Jobs.Application.Jobs.Commands.ScheduleJob;

internal sealed class ScheduleJobCommandHandler(
    IJobRepository repository,
    IDateTimeProvider clock)
    : ICommandHandler<ScheduleJobCommand>
{
    public async Task<Result> Handle(ScheduleJobCommand command, CancellationToken cancellationToken)
    {
        var jobId = new JobId(command.JobId);
        var job = await repository.GetByIdAsync(jobId, cancellationToken);
        if (job is null) return Result.Failure(JobErrors.NotFound(jobId));

        return job.Schedule(command.ScheduledDate, new AssigneeId(command.AssigneeId), clock.UtcNow);
    }
}
