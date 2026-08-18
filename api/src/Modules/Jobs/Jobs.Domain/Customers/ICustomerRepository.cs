using Jobs.Domain.Common;

namespace Jobs.Domain.Customers;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(CustomerId id, CancellationToken cancellationToken = default);
    Task<Customer?> FindByNormalizedNameAsync(OrganizationId organizationId, string normalizedName, CancellationToken cancellationToken = default);
    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
}
