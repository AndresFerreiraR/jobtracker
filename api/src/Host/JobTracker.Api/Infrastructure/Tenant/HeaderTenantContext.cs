using JobTracker.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace JobTracker.Api.Infrastructure.Tenant;

internal sealed class HeaderTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    private const string HeaderName = "X-Organization-Id";

    public Guid OrganizationId
    {
        get
        {
            var ctx = accessor.HttpContext
                ?? throw new InvalidOperationException("No HttpContext available.");

            if (!ctx.Request.Headers.TryGetValue(HeaderName, out var value) ||
                !Guid.TryParse(value, out var orgId))
            {
                throw new InvalidOperationException(
                    $"Missing or invalid '{HeaderName}' header (tenant identity required).");
            }
            return orgId;
        }
    }

    public bool IsPresent =>
        accessor.HttpContext?.Request.Headers.TryGetValue(HeaderName, out var v) == true
        && Guid.TryParse(v, out _);
}
