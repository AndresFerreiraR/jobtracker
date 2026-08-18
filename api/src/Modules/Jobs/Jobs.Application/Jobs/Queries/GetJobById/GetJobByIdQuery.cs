using JobTracker.BuildingBlocks.Application.Messaging;

namespace Jobs.Application.Jobs.Queries.GetJobById;

public sealed record GetJobByIdQuery(Guid JobId) : IQuery<JobDetailsDto>;
