using Jobs.Application.Customers.Queries;
using Jobs.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Jobs.Infrastructure.Persistence.Queries;

internal sealed class CustomerQueryService(JobsDbContext db) : ICustomerQueryService
{
    public async Task<IReadOnlyList<CustomerDto>> SearchAsync(
        Guid organizationId,
        string? query,
        int take,
        CancellationToken cancellationToken = default)
    {
        var orgId = new OrganizationId(organizationId);
        var q = db.Customers.AsNoTracking().Where(c => c.OrganizationId == orgId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query.Trim().ToLowerInvariant()}%";
            q = q.Where(c => EF.Functions.Like(c.NameNormalized, pattern));
        }

        var rows = await q
            .OrderBy(c => c.Name)
            .Take(take)
            .Select(c => new CustomerDto(c.Id.Value, c.Name, c.Email, c.Phone, c.CreatedAt))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<IReadOnlyList<CustomerDto>> GetByIdsAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return Array.Empty<CustomerDto>();
        var orgId = new OrganizationId(organizationId);
        var idSet = ids.Select(v => new CustomerId(v)).ToArray();

        return await db.Customers
            .AsNoTracking()
            .Where(c => c.OrganizationId == orgId && idSet.Contains(c.Id))
            .Select(c => new CustomerDto(c.Id.Value, c.Name, c.Email, c.Phone, c.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
