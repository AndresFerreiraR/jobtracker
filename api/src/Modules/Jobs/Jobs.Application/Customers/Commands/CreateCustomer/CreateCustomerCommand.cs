using JobTracker.BuildingBlocks.Application.Messaging;

namespace Jobs.Application.Customers.Commands.CreateCustomer;

public sealed record CreateCustomerCommand(
    string Name,
    string? Email,
    string? Phone) : ICommand<Guid>;
