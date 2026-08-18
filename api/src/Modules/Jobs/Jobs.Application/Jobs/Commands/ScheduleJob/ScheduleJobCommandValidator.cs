using FluentValidation;

namespace Jobs.Application.Jobs.Commands.ScheduleJob;

internal sealed class ScheduleJobCommandValidator : AbstractValidator<ScheduleJobCommand>
{
    public ScheduleJobCommandValidator()
    {
        RuleFor(x => x.JobId).NotEmpty();
        RuleFor(x => x.AssigneeId).NotEmpty();
        RuleFor(x => x.ScheduledDate).GreaterThan(DateTimeOffset.MinValue);
    }
}
