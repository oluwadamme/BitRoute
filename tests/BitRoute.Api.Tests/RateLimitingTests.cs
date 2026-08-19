using System.Net;
using System.Net.Http.Json;
using BitRoute.Application;
using BitRoute.Application.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace BitRoute.Api.Tests;

[Collection("IntegrationTests")]
public class RateLimitingTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _dbFile;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IAuthService> _authServiceMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();

    public RateLimitingTests(WebApplicationFactory<Program> factory)
    {
        _dbFile = $"test_ratelimit_{Guid.NewGuid():N}.db";

        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", $"Data Source={_dbFile}");
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "localhost:6379");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "bitroute");
        Environment.SetEnvironmentVariable("Jwt__Audience", "bitroute-clients");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "test-signing-key-that-is-long-enough-123456");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "60");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenDays", "30");
        Environment.SetEnvironmentVariable("Paystack__SecretKey", "sk_test_dummy_for_options_validation");
        Environment.SetEnvironmentVariable("Paystack__PublicKey", "pk_test_dummy_for_options_validation");
        Environment.SetEnvironmentVariable("Paystack__CallbackUrl", "http://localhost:3001/payment/callback");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IConnectionMultiplexer>();
                services.AddSingleton<IConnectionMultiplexer>(_redisMock.Object);

                services.RemoveAll<IAuthService>();
                services.AddScoped<IAuthService>(_ => _authServiceMock.Object);
            });
        });
    }

    public void Dispose()
    {
        _factory.Dispose();
        try
        {
            if (File.Exists(_dbFile)) File.Delete(_dbFile);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task AuthPolicy_ExceedingLimit_Returns429TooManyRequestsEnvelope()
    {
        var client = _factory.CreateClient();
        var request = new LoginRequest("test@example.com", "Password123!");

        _authServiceMock.Setup(s => s.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse.Success(new AuthResult("token", DateTimeOffset.UtcNow.AddHours(1), "refresh"), "Login successful."));

        HttpResponseMessage? lastResponse = null;

        // AuthPolicy permits 10 requests per minute
        for (int i = 0; i < 12; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/auth/login", request);
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);

        var envelope = await lastResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(envelope);
        Assert.False(envelope.Status);
        Assert.Equal("Rate limit exceeded. Please try again later.", envelope.Message);
    }
}
