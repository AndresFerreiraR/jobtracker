namespace Jobs.Application.Employees.Queries;

public sealed record EmployeeDto(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    DateTimeOffset CreatedAt);
