using System.Net;
using System.Net.Http.Json;
using JobTracker.Tests.Integration.Fixtures;

namespace JobTracker.Tests.Integration.Jobs;

[Trait("Category", "Integration")]
public sealed class JobsEndpointsTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private JobTrackerWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private readonly Guid _orgId = Guid.NewGuid();

    public JobsEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public Task InitializeAsync()
    {
        _factory = new JobTrackerWebApplicationFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Organization-Id", _orgId.ToString());
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Create_then_get_returns_the_job()
    {
        var body = new
        {
            title = "Roof repair",
            description = "Replace shingles",
            address = new
            {
                street = "123 Main",
                city = "Miami",
                state = "FL",
                zipCode = "33101",
                latitude = (decimal?)null,
                longitude = (decimal?)null,
            },
            customerId = Guid.NewGuid(),
        };

        var post = await _client.PostAsJsonAsync("/api/v1/jobs", body);
        post.StatusCode.Should().Be(HttpStatusCode.Created);

        var location = post.Headers.Location!.ToString();
        var get = await _client.GetAsync(location);
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await get.Content.ReadFromJsonAsync<JobDetailsResponse>();
        payload.Should().NotBeNull();
        payload!.Title.Should().Be("Roof repair");
        payload.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Create_with_invalid_zip_returns_400_ProblemDetails()
    {
        var body = new
        {
            title = "T",
            description = "",
            address = new { street = "s", city = "c", state = "st", zipCode = "ABC" },
            customerId = Guid.NewGuid(),
        };

        var post = await _client.PostAsJsonAsync("/api/v1/jobs", body);
        post.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        post.Content.Headers.ContentType?.MediaType.Should().BeOneOf("application/problem+json", "application/json");
    }

    [Fact]
    public async Task Missing_tenant_header_returns_500_problem()
    {
        _client.DefaultRequestHeaders.Remove("X-Organization-Id");

        var response = await _client.GetAsync($"/api/v1/jobs/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    private sealed record JobDetailsResponse(
        Guid Id,
        string Title,
        string Description,
        string Status);
}
