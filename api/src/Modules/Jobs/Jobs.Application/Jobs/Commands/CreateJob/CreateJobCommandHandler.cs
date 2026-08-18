using Jobs.Domain.Common;
using Jobs.Domain.Jobs;
using JobTracker.BuildingBlocks.Application.Abstractions;
using JobTracker.BuildingBlocks.Application.Messaging;
using JobTracker.SharedKernel.Results;

namespace Jobs.Application.Jobs.Commands.CreateJob;

internal sealed class CreateJobCommandHandler(
    IJobRepository repository,
    ITenantContext tenant,
    IDateTimeProvider clock)
    : ICommandHandler<CreateJobCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateJobCommand command, CancellationToken cancellationToken)
    {
        var addressResult = Address.Create(
            command.Address.Street,
            command.Address.City,
            command.Address.State,
            command.Address.ZipCode,
            command.Address.Latitude,
            command.Address.Longitude);

        if (addressResult.IsFailure)
            return addressResult.Error;

        var jobResult = Job.Create(
            new OrganizationId(tenant.OrganizationId),
            command.Title,
            command.Description,
            addressResult.Value!,
            new CustomerId(command.CustomerId),
            clock.UtcNow);

        if (jobResult.IsFailure)
            return jobResult.Error;

        await repository.AddAsync(jobResult.Value!, cancellationToken);
        return jobResult.Value!.Id.Value;
    }
}
