namespace Jobs.Application.Customers.Queries;

public sealed record CustomerDto(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    DateTimeOffset CreatedAt);
