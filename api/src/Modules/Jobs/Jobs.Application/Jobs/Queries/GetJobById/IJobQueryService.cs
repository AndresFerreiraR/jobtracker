using Jobs.Application.Jobs.Queries.ListJobs;

namespace Jobs.Application.Jobs.Queries.GetJobById;

public interface IJobQueryService
{
    Task<JobDetailsDto?> GetByIdAsync(
        Guid organizationId,
        Guid jobId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<JobListItemDto> Items, string? NextCursor)> ListAsync(
        Guid organizationId,
        ListJobsFilter filter,
        CancellationToken cancellationToken = default);
}
