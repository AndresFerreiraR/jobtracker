using FluentValidation;
using Jobs.Domain.Common;
using Jobs.Domain.Jobs;
using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Application.Messaging;
using JobTracker.SharedKernel.Results;

namespace Jobs.Application.Jobs.Commands.CancelJob;

public sealed record CancelJobCommand(Guid JobId, string Reason) : ICommand;

internal sealed class CancelJobCommandValidator : AbstractValidator<CancelJobCommand>
{
    public CancelJobCommandValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

internal sealed class CancelJobCommandHandler(
    IJobRepository repository,
    IDateTimeProvider clock)
    : ICommandHandler<CancelJobCommand>
{
    public async Task<Result> Handle(CancelJobCommand command, CancellationToken cancellationToken)
    {
        var jobId = new JobId(command.JobId);
        var job = await repository.GetByIdAsync(jobId, cancellationToken);
        if (job is null) return Result.Failure(JobErrors.NotFound(jobId));

        return job.Cancel(command.Reason, clock.UtcNow);
    }
}
