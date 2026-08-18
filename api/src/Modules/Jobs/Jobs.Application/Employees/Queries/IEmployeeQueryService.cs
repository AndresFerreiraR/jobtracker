namespace Jobs.Application.Employees.Queries;

public interface IEmployeeQueryService
{
    Task<IReadOnlyList<EmployeeDto>> SearchAsync(
        Guid organizationId,
        string? query,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeDto>> GetByIdsAsync(
        Guid organizationId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}
