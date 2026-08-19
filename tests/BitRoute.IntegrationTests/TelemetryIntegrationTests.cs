using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace BitRoute.IntegrationTests;

public class TelemetryIntegrationTests : IClassFixture<BitRouteTestFactory>
{
    private readonly HttpClient _client;

    public TelemetryIntegrationTests(BitRouteTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetLatestTelemetry_Returns_NotFound_Or_Success_Payload()
    {
        // Act
        var randomScheduleId = Guid.NewGuid();
        var response = await _client.GetAsync($"/schedules/{randomScheduleId}/telemetry/latest");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.GetProperty("status").GetBoolean());
    }
}
