using System.Security.Claims;
using JobTracker.Api.Infrastructure.Auth;
using JobTracker.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace JobTracker.Api.Infrastructure.Tenant;

internal sealed class JwtTenantContext(
    IHttpContextAccessor accessor,
    IOptions<JwtOptions> jwt) : ITenantContext
{
    private readonly JwtOptions _jwt = jwt.Value;

    public Guid OrganizationId
    {
        get
        {
            if (!TryExtract(out var orgId))
                throw new MissingTenantException(
                    $"No tenant identity in request (missing '{_jwt.OrganizationClaim}' claim or 'X-Organization-Id' header).");
            return orgId;
        }
    }

    public bool IsPresent => TryExtract(out _);

    private bool TryExtract(out Guid orgId)
    {
        orgId = Guid.Empty;
        var ctx = accessor.HttpContext;
        if (ctx is null) return false;

        var user = ctx.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var claim = user.FindFirstValue(_jwt.OrganizationClaim);
            if (Guid.TryParse(claim, out var fromClaim))
            {
                orgId = fromClaim;
                return true;
            }
        }

        if (ctx.Request.Headers.TryGetValue("X-Organization-Id", out var v) &&
            Guid.TryParse(v, out var fromHeader))
        {
            orgId = fromHeader;
            return true;
        }

        return false;
    }
}
