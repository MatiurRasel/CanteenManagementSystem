// =============================================================================
// HttpSmokeTests — surface checks through the full middleware pipeline
// (correlation id, status pages, security headers, rate limiter, tenant
// middleware, auth) against the migrated + seeded database.
// =============================================================================

using System.Net;
using FluentAssertions;
using Xunit;

namespace CanteenManagementSystem.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class HttpSmokeTests
{
    private readonly CanteenWebApplicationFactory _factory;

    public HttpSmokeTests(CanteenWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task AccountLogin_ReturnsHtmlLoginPage()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/Account/Login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("<html", "the login page must render full HTML");
    }

    [Fact]
    public async Task HealthLive_Returns200()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task PublicMenu_IsAnonymouslyReachable()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/menu");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }
}
