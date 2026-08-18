using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Application.Messaging;
using JobTracker.SharedKernel.Results;

namespace Jobs.Application.Customers.Queries.ListCustomers;

public sealed record ListCustomersQuery(string? Query, int Take = 20)
    : IQuery<IReadOnlyList<CustomerDto>>;

internal sealed class ListCustomersQueryHandler(
    ICustomerQueryService queries,
    ITenantContext tenant)
    : IQueryHandler<ListCustomersQuery, IReadOnlyList<CustomerDto>>
{
    public async Task<Result<IReadOnlyList<CustomerDto>>> Handle(
        ListCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var take = request.Take is < 1 or > 50 ? 20 : request.Take;
        var items = await queries.SearchAsync(tenant.OrganizationId, request.Query, take, cancellationToken);
        return Result<IReadOnlyList<CustomerDto>>.Success(items);
    }
}
