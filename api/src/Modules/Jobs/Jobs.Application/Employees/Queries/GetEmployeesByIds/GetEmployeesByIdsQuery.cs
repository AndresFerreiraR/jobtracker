using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Application.Messaging;
using JobTracker.SharedKernel.Results;

namespace Jobs.Application.Employees.Queries.GetEmployeesByIds;

public sealed record GetEmployeesByIdsQuery(IReadOnlyCollection<Guid> Ids)
    : IQuery<IReadOnlyList<EmployeeDto>>;

internal sealed class GetEmployeesByIdsQueryHandler(
    IEmployeeQueryService queries,
    ITenantContext tenant)
    : IQueryHandler<GetEmployeesByIdsQuery, IReadOnlyList<EmployeeDto>>
{
    public async Task<Result<IReadOnlyList<EmployeeDto>>> Handle(
        GetEmployeesByIdsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Ids.Count == 0)
            return Result<IReadOnlyList<EmployeeDto>>.Success(Array.Empty<EmployeeDto>());
        var items = await queries.GetByIdsAsync(tenant.OrganizationId, request.Ids, cancellationToken);
        return Result<IReadOnlyList<EmployeeDto>>.Success(items);
    }
}
