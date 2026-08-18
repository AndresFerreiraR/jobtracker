namespace JobTracker.Api.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Authority { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string OrganizationClaim { get; init; } = "org_id";
    public bool RequireHttpsMetadata { get; init; } = true;
    public bool Enabled { get; init; } = true;
}
