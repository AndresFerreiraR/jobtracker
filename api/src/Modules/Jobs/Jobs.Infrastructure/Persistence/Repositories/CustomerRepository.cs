using Jobs.Domain.Common;
using Jobs.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Jobs.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository(JobsDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(CustomerId id, CancellationToken cancellationToken = default) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Customer?> FindByNormalizedNameAsync(
        OrganizationId organizationId,
        string normalizedName,
        CancellationToken cancellationToken = default) =>
        db.Customers.FirstOrDefaultAsync(
            c => c.OrganizationId == organizationId && c.NameNormalized == normalizedName,
            cancellationToken);

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default) =>
        await db.Customers.AddAsync(customer, cancellationToken);
}
