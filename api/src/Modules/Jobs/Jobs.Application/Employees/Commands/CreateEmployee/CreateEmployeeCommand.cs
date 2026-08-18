using JobTracker.BuildingBlocks.Application.Messaging;

namespace Jobs.Application.Employees.Commands.CreateEmployee;

public sealed record CreateEmployeeCommand(
    string Name,
    string? Email,
    string? Phone) : ICommand<Guid>;
