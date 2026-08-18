using System.Reflection;
using NetArchTest.Rules;

namespace JobTracker.Tests.Architecture;

public sealed class LayeringRulesTests
{
    private static readonly Assembly SharedKernel = typeof(JobTracker.SharedKernel.Primitives.Entity<>).Assembly;
    private static readonly Assembly BuildingBlocksApplication =
        typeof(JobTracker.BuildingBlocks.Application.Messaging.ICommand).Assembly;
    private static readonly Assembly BuildingBlocksInfrastructure =
        Assembly.Load("JobTracker.BuildingBlocks.Infrastructure");
    private static readonly Assembly BuildingBlocksPresentation =
        typeof(JobTracker.BuildingBlocks.Presentation.ApiControllerBase).Assembly;

    private static readonly Assembly JobsDomain = typeof(Jobs.Domain.Jobs.Job).Assembly;
    private static readonly Assembly JobsApplication = typeof(Jobs.Application.AssemblyMarker).Assembly;
    private static readonly Assembly JobsInfrastructure =
        typeof(Jobs.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly JobsPresentation =
        typeof(Jobs.Presentation.PresentationAssemblyMarker).Assembly;
    private static readonly Assembly JobsIntegrationEvents =
        typeof(Jobs.IntegrationEvents.JobCreatedIntegrationEvent).Assembly;

    [Fact]
    public void SharedKernel_does_not_depend_on_any_other_JobTracker_layer()
    {
        var result = Types.InAssembly(SharedKernel)
            .Should()
            .NotHaveDependencyOnAny(
                "JobTracker.BuildingBlocks.Application",
                "JobTracker.BuildingBlocks.Infrastructure",
                "JobTracker.BuildingBlocks.Presentation",
                "Jobs.Domain",
                "Jobs.Application",
                "Jobs.Infrastructure",
                "Jobs.Presentation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "SharedKernel is the innermost layer and must not depend on any other JobTracker assembly");
    }

    [Fact]
    public void Jobs_Domain_does_not_depend_on_Application_Infrastructure_or_Presentation()
    {
        var result = Types.InAssembly(JobsDomain)
            .Should()
            .NotHaveDependencyOnAny(
                "Jobs.Application",
                "Jobs.Infrastructure",
                "Jobs.Presentation",
                "JobTracker.BuildingBlocks.Application",
                "JobTracker.BuildingBlocks.Infrastructure",
                "JobTracker.BuildingBlocks.Presentation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain must not depend on outer layers or on any Application/Presentation building block");
    }

    [Fact]
    public void Jobs_Domain_does_not_reference_EntityFrameworkCore()
    {
        var result = Types.InAssembly(JobsDomain)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain must remain persistence-agnostic");
    }

    [Fact]
    public void Jobs_Domain_does_not_reference_AspNetCore()
    {
        var result = Types.InAssembly(JobsDomain)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain must remain framework-agnostic");
    }

    [Fact]
    public void Jobs_Application_does_not_depend_on_Infrastructure_or_Presentation()
    {
        var result = Types.InAssembly(JobsApplication)
            .Should()
            .NotHaveDependencyOnAny("Jobs.Infrastructure", "Jobs.Presentation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application depends on Domain and abstractions only, never on outer layers");
    }

    [Fact]
    public void Jobs_Application_does_not_reference_EntityFrameworkCore()
    {
        var result = Types.InAssembly(JobsApplication)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application must talk to storage only through IJobRepository / IJobQueryService");
    }

    [Fact]
    public void Jobs_IntegrationEvents_depends_only_on_SharedKernel_and_MediatR()
    {
        var result = Types.InAssembly(JobsIntegrationEvents)
            .Should()
            .NotHaveDependencyOnAny(
                "Jobs.Domain",
                "Jobs.Application",
                "Jobs.Infrastructure",
                "Jobs.Presentation",
                "JobTracker.BuildingBlocks.Application",
                "JobTracker.BuildingBlocks.Infrastructure",
                "JobTracker.BuildingBlocks.Presentation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Integration events are a stable public contract that must not leak internal types");
    }

    [Fact]
    public void Jobs_Presentation_does_not_reference_Infrastructure()
    {
        var result = Types.InAssembly(JobsPresentation)
            .Should()
            .NotHaveDependencyOnAny("Jobs.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Controllers must dispatch through MediatR, never touch Infrastructure directly");
    }

    [Fact]
    public void Command_handlers_are_internal_and_sealed()
    {
        var result = Types.InAssembly(JobsApplication)
            .That()
            .HaveNameEndingWith("CommandHandler")
            .Should()
            .NotBePublic()
            .And()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Handlers should be internal sealed. Offending: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }
}
