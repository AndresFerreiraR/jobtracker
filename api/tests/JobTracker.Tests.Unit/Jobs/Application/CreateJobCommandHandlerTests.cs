using Jobs.Application.Jobs.Commands.CreateJob;
using Jobs.Domain.Common;
using Jobs.Domain.Jobs;
using JobTracker.BuildingBlocks.Application.Abstractions;
using NSubstitute;

namespace JobTracker.Tests.Unit.Jobs.Application;

public sealed class CreateJobCommandHandlerTests
{
    private readonly IJobRepository _repository = Substitute.For<IJobRepository>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    public CreateJobCommandHandlerTests()
    {
        _tenant.OrganizationId.Returns(Guid.NewGuid());
        _clock.UtcNow.Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_returns_success_with_new_id_and_adds_job()
    {
        var handler = new CreateJobCommandHandler(_repository, _tenant, _clock);
        var command = new CreateJobCommand(
            Title: "Roof repair",
            Description: "Replace shingles",
            Address: new AddressDto("123 Main", "Miami", "FL", "33101", null, null),
            CustomerId: Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
        await _repository.Received(1).AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_validation_error_when_address_zip_is_invalid()
    {
        var handler = new CreateJobCommandHandler(_repository, _tenant, _clock);
        var command = new CreateJobCommand(
            Title: "T",
            Description: "D",
            Address: new AddressDto("123", "M", "FL", "AB@1", null, null),
            CustomerId: Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Address.InvalidZipCode");
        await _repository.DidNotReceive().AddAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_validation_error_when_title_is_empty()
    {
        var handler = new CreateJobCommandHandler(_repository, _tenant, _clock);
        var command = new CreateJobCommand(
            Title: "",
            Description: "D",
            Address: new AddressDto("123", "M", "FL", "33101", null, null),
            CustomerId: Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Job.InvalidTitle");
    }
}
