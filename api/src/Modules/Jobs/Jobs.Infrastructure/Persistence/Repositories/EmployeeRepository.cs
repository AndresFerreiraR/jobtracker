using Jobs.Domain.Common;
using Jobs.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace Jobs.Infrastructure.Persistence.Repositories;

internal sealed class EmployeeRepository(JobsDbContext db) : IEmployeeRepository
{
    public Task<Employee?> GetByIdAsync(EmployeeId id, CancellationToken cancellationToken = default) =>
        db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<Employee?> FindByNormalizedNameAsync(
        OrganizationId organizationId,
        string normalizedName,
        CancellationToken cancellationToken = default) =>
        db.Employees.FirstOrDefaultAsync(
            e => e.OrganizationId == organizationId && e.NameNormalized == normalizedName,
            cancellationToken);

    public async Task AddAsync(Employee employee, CancellationToken cancellationToken = default) =>
        await db.Employees.AddAsync(employee, cancellationToken);
}
