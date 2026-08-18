namespace Jobs.Presentation.Contracts;

public sealed record CreateCustomerRequest(string Name, string? Email, string? Phone);

public sealed record GetCustomersByIdsRequest(IReadOnlyCollection<Guid> Ids);
