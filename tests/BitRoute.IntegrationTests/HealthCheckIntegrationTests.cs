using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace BitRoute.IntegrationTests;

public class HealthCheckIntegrationTests : IClassFixture<BitRouteTestFactory>
{
    private readonly HttpClient _client;

    public HealthCheckIntegrationTests(BitRouteTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_Returns_200_OK_And_Healthy_Status()
    {
        // Act
        var response = await _client.GetAsync("/healthz");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", json.GetProperty("status").GetString());

        var checks = json.GetProperty("checks").EnumerateArray().ToList();
        Assert.Contains(checks, c => c.GetProperty("name").GetString() == "postgresql" && c.GetProperty("status").GetString() == "Healthy");
        Assert.Contains(checks, c => c.GetProperty("name").GetString() == "redis" && c.GetProperty("status").GetString() == "Healthy");
    }
}
