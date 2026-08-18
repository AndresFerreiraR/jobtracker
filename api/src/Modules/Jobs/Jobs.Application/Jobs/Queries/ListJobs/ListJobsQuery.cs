using Jobs.Application.Jobs.Queries.GetJobById;
using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Application.Messaging;
using JobTracker.BuildingBlocks.Application.Pagination;
using JobTracker.SharedKernel.Results;

namespace Jobs.Application.Jobs.Queries.ListJobs;

public sealed record ListJobsFilter(
    string? Status,
    Guid? AssigneeId,
    Guid? CustomerId,
    DateTimeOffset? ScheduledFrom,
    DateTimeOffset? ScheduledTo,
    string? Cursor,
    int PageSize);

public sealed record ListJobsQuery(
    string? Status,
    Guid? AssigneeId,
    Guid? CustomerId,
    DateTimeOffset? ScheduledFrom,
    DateTimeOffset? ScheduledTo,
    string? Cursor,
    int PageSize = 25) : IQuery<PagedList<JobListItemDto>>;

internal sealed class ListJobsQueryHandler(
    IJobQueryService queries,
    ITenantContext tenant)
    : IQueryHandler<ListJobsQuery, PagedList<JobListItemDto>>
{
    public async Task<Result<PagedList<JobListItemDto>>> Handle(
        ListJobsQuery request,
        CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize is < 1 or > 100 ? 25 : request.PageSize;
        var filter = new ListJobsFilter(
            request.Status,
            request.AssigneeId,
            request.CustomerId,
            request.ScheduledFrom,
            request.ScheduledTo,
            request.Cursor,
            pageSize);

        var (items, next) = await queries.ListAsync(tenant.OrganizationId, filter, cancellationToken);
        return new PagedList<JobListItemDto>(items, next);
    }
}
