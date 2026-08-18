using Jobs.Domain.Common;
using Jobs.Domain.Jobs;
using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Application.Messaging;
using JobTracker.SharedKernel.Results;

namespace Jobs.Application.Jobs.Queries.GetJobById;

internal sealed class GetJobByIdQueryHandler(
    IJobQueryService queries,
    ITenantContext tenant)
    : IQueryHandler<GetJobByIdQuery, JobDetailsDto>
{
    public async Task<Result<JobDetailsDto>> Handle(GetJobByIdQuery query, CancellationToken cancellationToken)
    {
        var dto = await queries.GetByIdAsync(tenant.OrganizationId, query.JobId, cancellationToken);
        return dto is null
            ? JobErrors.NotFound(new JobId(query.JobId))
            : dto;
    }
}
