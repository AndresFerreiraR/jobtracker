namespace Jobs.Presentation.Contracts;

public sealed record CreateEmployeeRequest(string Name, string? Email, string? Phone);

public sealed record GetEmployeesByIdsRequest(IReadOnlyCollection<Guid> Ids);
