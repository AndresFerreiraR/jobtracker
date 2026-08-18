namespace Jobs.Application.Customers.Queries;

public interface ICustomerQueryService
{
    Task<IReadOnlyList<CustomerDto>> SearchAsync(
        Guid organizationId,
        string? query,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerDto>> GetByIdsAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}
