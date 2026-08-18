using Jobs.Domain.Common;
using Jobs.Domain.Customers;
using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Application.Messaging;
using JobTracker.SharedKernel.Results;

namespace Jobs.Application.Customers.Commands.CreateCustomer;

internal sealed class CreateCustomerCommandHandler(
    ICustomerRepository repository,
    ITenantContext tenant,
    IDateTimeProvider clock)
    : ICommandHandler<CreateCustomerCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        var orgId = new OrganizationId(tenant.OrganizationId);
        var normalized = command.Name?.Trim().ToLowerInvariant() ?? string.Empty;

        var existing = await repository.FindByNormalizedNameAsync(orgId, normalized, cancellationToken);
        if (existing is not null)
            return existing.Id.Value;

        var result = Customer.Create(orgId, command.Name, command.Email, command.Phone, clock.UtcNow);
        if (result.IsFailure) return result.Error;

        await repository.AddAsync(result.Value!, cancellationToken);
        return result.Value!.Id.Value;
    }
}
