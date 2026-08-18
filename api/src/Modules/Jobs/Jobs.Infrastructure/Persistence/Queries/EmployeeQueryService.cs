using Jobs.Application.Employees.Queries;
using Jobs.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Jobs.Infrastructure.Persistence.Queries;

internal sealed class EmployeeQueryService(JobsDbContext db) : IEmployeeQueryService
{
    public async Task<IReadOnlyList<EmployeeDto>> SearchAsync(
        Guid organizationId,
        string? query,
        int take,
        CancellationToken cancellationToken = default)
    {
        var orgId = new OrganizationId(organizationId);
        var q = db.Employees.AsNoTracking().Where(e => e.OrganizationId == orgId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query.Trim().ToLowerInvariant()}%";
            q = q.Where(e => EF.Functions.Like(e.NameNormalized, pattern));
        }

        var rows = await q
            .OrderBy(e => e.Name)
            .Take(take)
            .Select(e => new EmployeeDto(e.Id.Value, e.Name, e.Email, e.Phone, e.CreatedAt))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<IReadOnlyList<EmployeeDto>> GetByIdsAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return Array.Empty<EmployeeDto>();
        var orgId = new OrganizationId(organizationId);
        var idSet = ids.Select(v => new EmployeeId(v)).ToArray();

        return await db.Employees
            .AsNoTracking()
            .Where(e => e.OrganizationId == orgId && idSet.Contains(e.Id))
            .Select(e => new EmployeeDto(e.Id.Value, e.Name, e.Email, e.Phone, e.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
