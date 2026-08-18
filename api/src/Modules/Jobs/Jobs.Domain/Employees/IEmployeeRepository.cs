using Jobs.Domain.Common;

namespace Jobs.Domain.Employees;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(EmployeeId id, CancellationToken cancellationToken = default);
    Task<Employee?> FindByNormalizedNameAsync(OrganizationId organizationId, string normalizedName, CancellationToken cancellationToken = default);
    Task AddAsync(Employee employee, CancellationToken cancellationToken = default);
}
