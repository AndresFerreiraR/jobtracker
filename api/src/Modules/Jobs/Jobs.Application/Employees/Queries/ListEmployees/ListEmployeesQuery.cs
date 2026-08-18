using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Application.Messaging;
using JobTracker.SharedKernel.Results;

namespace Jobs.Application.Employees.Queries.ListEmployees;

public sealed record ListEmployeesQuery(string? Query, int Take = 20)
    : IQuery<IReadOnlyList<EmployeeDto>>;

internal sealed class ListEmployeesQueryHandler(
    IEmployeeQueryService queries,
    ITenantContext tenant)
    : IQueryHandler<ListEmployeesQuery, IReadOnlyList<EmployeeDto>>
{
    public async Task<Result<IReadOnlyList<EmployeeDto>>> Handle(
        ListEmployeesQuery request,
        CancellationToken cancellationToken)
    {
        var take = request.Take is < 1 or > 50 ? 20 : request.Take;
        var items = await queries.SearchAsync(tenant.OrganizationId, request.Query, take, cancellationToken);
        return Result<IReadOnlyList<EmployeeDto>>.Success(items);
    }
}
