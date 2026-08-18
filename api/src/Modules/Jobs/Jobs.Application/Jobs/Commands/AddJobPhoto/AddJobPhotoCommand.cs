using FluentValidation;
using Jobs.Domain.Common;
using Jobs.Domain.Jobs;
using JobTracker.BuildingBlocks.Application.Messaging;
using JobTracker.SharedKernel.Results;

namespace Jobs.Application.Jobs.Commands.AddJobPhoto;

public sealed record AddJobPhotoCommand(
    Guid JobId,
    string Url,
    DateTimeOffset CapturedAt,
    string? Caption) : ICommand<Guid>;

internal sealed class AddJobPhotoCommandValidator : AbstractValidator<AddJobPhotoCommand>
{
    public AddJobPhotoCommandValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.Url).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Caption).MaximumLength(500);
    }
}

internal sealed class AddJobPhotoCommandHandler(IJobRepository repository)
    : ICommandHandler<AddJobPhotoCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddJobPhotoCommand command, CancellationToken cancellationToken)
    {
        var jobId = new JobId(command.JobId);
        var job = await repository.GetByIdAsync(jobId, cancellationToken);
        if (job is null) return JobErrors.NotFound(jobId);

        var photoResult = job.AddPhoto(command.Url, command.CapturedAt, command.Caption);
        return photoResult.IsFailure
            ? photoResult.Error
            : photoResult.Value.Value;
    }
}
