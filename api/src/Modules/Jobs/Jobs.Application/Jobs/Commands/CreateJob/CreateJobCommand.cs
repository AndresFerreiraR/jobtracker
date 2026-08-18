using JobTracker.BuildingBlocks.Application.Messaging;

namespace Jobs.Application.Jobs.Commands.CreateJob;

public sealed record CreateJobCommand(
    string Title,
    string Description,
    AddressDto Address,
    Guid CustomerId) : ICommand<Guid>;

public sealed record AddressDto(
    string Street,
    string City,
    string State,
    string ZipCode,
    decimal? Latitude,
    decimal? Longitude);
