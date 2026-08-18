using Jobs.Application.Jobs.Queries;
using Jobs.Application.Jobs.Queries.GetJobById;
using Jobs.Application.Jobs.Queries.ListJobs;
using Jobs.Domain.Common;
using Jobs.Domain.Jobs;
using JobTracker.BuildingBlocks.Application.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Jobs.Infrastructure.Persistence.Queries;

internal sealed class JobQueryService(JobsDbContext db) : IJobQueryService
{
    public async Task<JobDetailsDto?> GetByIdAsync(
        Guid organizationId,
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var id = new JobId(jobId);
        var orgId = new OrganizationId(organizationId);

        var job = await db.Jobs
            .AsNoTracking()
            .Include(j => j.Photos)
            .Where(j => j.OrganizationId == orgId && j.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        return job is null ? null : ToDetails(job);
    }

    public async Task<(IReadOnlyList<JobListItemDto> Items, string? NextCursor)> ListAsync(
        Guid organizationId,
        ListJobsFilter filter,
        CancellationToken cancellationToken = default)
    {
        var orgId = new OrganizationId(organizationId);
        var query = db.Jobs.AsNoTracking().Where(j => j.OrganizationId == orgId);

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            Enum.TryParse<JobStatus>(filter.Status, ignoreCase: true, out var status))
        {
            query = query.Where(j => j.Status == status);
        }

        if (filter.AssigneeId is { } aid)
            query = query.Where(j => j.AssigneeId == new AssigneeId(aid));

        if (filter.CustomerId is { } cid)
            query = query.Where(j => j.CustomerId == new CustomerId(cid));

        if (filter.ScheduledFrom is { } from)
            query = query.Where(j => j.ScheduledDate >= from);

        if (filter.ScheduledTo is { } to)
            query = query.Where(j => j.ScheduledDate <= to);

        var cursor = Cursor.TryDecode(filter.Cursor);
        if (cursor is not null)
        {
            query = query.Where(j => j.CreatedAt < cursor.CreatedAt);
        }

        var pageSize = filter.PageSize;
        var page = await query
            .OrderByDescending(j => j.CreatedAt)
            .ThenByDescending(j => j.Id)
            .Take(pageSize + 1)
            .Select(j => new
            {
                j.Id,
                j.Title,
                j.Status,
                j.CustomerId,
                j.AssigneeId,
                j.ScheduledDate,
                j.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var hasMore = page.Count > pageSize;
        var visible = hasMore ? page.Take(pageSize).ToList() : page;

        var items = visible
            .Select(j => new JobListItemDto(
                j.Id.Value,
                j.Title,
                j.Status.ToString(),
                j.CustomerId.Value,
                j.AssigneeId?.Value,
                j.ScheduledDate,
                j.CreatedAt))
            .ToArray();

        string? nextCursor = hasMore
            ? new Cursor(visible[^1].CreatedAt, visible[^1].Id.Value).Encode()
            : null;

        return (items, nextCursor);
    }

    private static JobDetailsDto ToDetails(Job j) => new(
        j.Id.Value,
        j.Title,
        j.Description,
        new JobAddressDto(
            j.Address.Street,
            j.Address.City,
            j.Address.State,
            j.Address.ZipCode,
            j.Address.Latitude,
            j.Address.Longitude),
        j.Status.ToString(),
        j.ScheduledDate,
        j.StartedAt,
        j.CompletedAt,
        j.CancelledAt,
        j.CancellationReason,
        j.SignatureUrl,
        j.AssigneeId?.Value,
        j.CustomerId.Value,
        j.CreatedAt,
        j.UpdatedAt,
        j.Photos
            .OrderByDescending(p => p.CapturedAt)
            .Select(p => new JobPhotoDto(p.Id.Value, p.Url, p.CapturedAt, p.Caption))
            .ToArray());
}
