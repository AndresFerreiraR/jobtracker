using FluentValidation;
using Jobs.Domain.Common;
using Jobs.Domain.Jobs;
using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Application.Messaging;
using JobTracker.SharedKernel.Results;

namespace Jobs.Application.Jobs.Commands.CompleteJob;

public sealed record CompleteJobCommand(Guid JobId, string SignatureUrl) : ICommand;

internal sealed class CompleteJobCommandValidator : AbstractValidator<CompleteJobCommand>
{
    public CompleteJobCommandValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.SignatureUrl).NotEmpty().MaximumLength(1000);
    }
}

internal sealed class CompleteJobCommandHandler(
    IJobRepository repository,
    IDateTimeProvider clock)
    : ICommandHandler<CompleteJobCommand>
{
    public async Task<Result> Handle(CompleteJobCommand command, CancellationToken cancellationToken)
    {
        var jobId = new JobId(command.JobId);
        var job = await repository.GetByIdAsync(jobId, cancellationToken);
        if (job is null) return Result.Failure(JobErrors.NotFound(jobId));

        return job.Complete(command.SignatureUrl, clock.UtcNow);
    }
}
