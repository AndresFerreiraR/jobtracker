namespace JobTracker.BuildingBlocks.Application.Abstractions;

public interface ITenantContext
{
    Guid OrganizationId { get; }
    bool IsPresent { get; }
}
