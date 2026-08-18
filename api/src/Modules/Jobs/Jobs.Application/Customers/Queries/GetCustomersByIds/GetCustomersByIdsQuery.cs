using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Application.Messaging;
using JobTracker.SharedKernel.Results;

namespace Jobs.Application.Customers.Queries.GetCustomersByIds;

public sealed record GetCustomersByIdsQuery(IReadOnlyCollection<Guid> Ids)
    : IQuery<IReadOnlyList<CustomerDto>>;

internal sealed class GetCustomersByIdsQueryHandler(
    ICustomerQueryService queries,
    ITenantContext tenant)
    : IQueryHandler<GetCustomersByIdsQuery, IReadOnlyList<CustomerDto>>
{
    public async Task<Result<IReadOnlyList<CustomerDto>>> Handle(
        GetCustomersByIdsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Ids.Count == 0)
            return Result<IReadOnlyList<CustomerDto>>.Success(Array.Empty<CustomerDto>());
        var items = await queries.GetByIdsAsync(tenant.OrganizationId, request.Ids, cancellationToken);
        return Result<IReadOnlyList<CustomerDto>>.Success(items);
    }
}
