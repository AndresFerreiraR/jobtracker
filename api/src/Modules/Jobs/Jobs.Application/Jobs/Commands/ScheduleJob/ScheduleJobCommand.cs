using JobTracker.BuildingBlocks.Application.Messaging;

namespace Jobs.Application.Jobs.Commands.ScheduleJob;

public sealed record ScheduleJobCommand(Guid JobId, DateTimeOffset ScheduledDate, Guid AssigneeId) : ICommand;
