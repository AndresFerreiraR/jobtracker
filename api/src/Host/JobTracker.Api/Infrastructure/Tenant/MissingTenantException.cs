namespace JobTracker.Api.Infrastructure.Tenant;

public sealed class MissingTenantException(string message) : InvalidOperationException(message);
