using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BitRoute.Application;
using BitRoute.Application.Auth;
using BitRoute.Domain.Enums;
using BitRoute.Domain.Interfaces;
using BitRoute.Domain.ValueObjects;
using BitRoute.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace BitRoute.Api.Tests;

public class AuthControllerTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _dbFile;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IAuthService> _authServiceMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();

    public AuthControllerTests(WebApplicationFactory<Program> factory)
    {
        _dbFile = $"test_auth_{Guid.NewGuid():N}.db";

        // Set environment variables before host builds to configure SQLite and bypass validations
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", $"Data Source={_dbFile}");
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "localhost:6379");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "bitroute");
        Environment.SetEnvironmentVariable("Jwt__Audience", "bitroute-clients");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "test-signing-key-that-is-long-enough-123456");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "60");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenDays", "30");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Replace Redis multiplexer with a mock
                services.RemoveAll<IConnectionMultiplexer>();
                services.AddSingleton<IConnectionMultiplexer>(_redisMock.Object);

                // Mock out the IAuthService
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
            // Ignore if file is locked
        }
    }

    private HttpClient CreateAuthenticatedClient(UserRole role)
    {
        var tokenGenerator = _factory.Services.GetRequiredService<ITokenGenerator>();
        var accessToken = tokenGenerator.GenerateAccessToken(Guid.NewGuid(), "user@bitroute.com", [role]);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        return client;
    }

    [Fact]
    public async Task GetMe_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_Authenticated_ReturnsOk()
    {
        var client = CreateAuthenticatedClient(UserRole.Passenger);

        var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_ReturnsSuccess()
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest("new@example.com", "Password123!", "New User");
        var expectedResult = new AuthResult("access-token", DateTimeOffset.UtcNow.AddHours(1), "refresh-token");
        
        _authServiceMock.Setup(s => s.RegisterAsync(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse.Success(expectedResult, "Account created."));

        var response = await client.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResult>>();
        Assert.NotNull(envelope);
        Assert.True(envelope.Status);
        Assert.Equal("access-token", envelope.Data!.AccessToken);
    }

    [Fact]
    public async Task Login_ReturnsSuccess()
    {
        var client = _factory.CreateClient();
        var request = new LoginRequest("ada@example.com", "Password123!");
        var expectedResult = new AuthResult("access-token", DateTimeOffset.UtcNow.AddHours(1), "refresh-token");

        _authServiceMock.Setup(s => s.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse.Success(expectedResult, "Login successful."));

        var response = await client.PostAsJsonAsync("/auth/login", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResult>>();
        Assert.NotNull(envelope);
        Assert.True(envelope.Status);
        Assert.Equal("access-token", envelope.Data!.AccessToken);
    }

    [Fact]
    public async Task Refresh_ReturnsSuccess()
    {
        var client = _factory.CreateClient();
        var request = new RefreshRequest("old-refresh-token");
        var expectedResult = new AuthResult("new-access-token", DateTimeOffset.UtcNow.AddHours(1), "new-refresh-token");

        _authServiceMock.Setup(s => s.RefreshAsync(It.IsAny<RefreshRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse.Success(expectedResult, "Token refreshed."));

        var response = await client.PostAsJsonAsync("/auth/refresh", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResult>>();
        Assert.NotNull(envelope);
        Assert.True(envelope.Status);
        Assert.Equal("new-access-token", envelope.Data!.AccessToken);
    }

    [Fact]
    public async Task Logout_ReturnsSuccess()
    {
        var client = _factory.CreateClient();
        var request = new LogoutRequest("refresh-token");

        _authServiceMock.Setup(s => s.LogoutAsync(It.IsAny<LogoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse.SuccessMessage("Logged out."));

        var response = await client.PostAsJsonAsync("/auth/logout", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(envelope);
        Assert.True(envelope.Status);
        Assert.Equal("Logged out.", envelope.Message);
    }

    [Fact]
    public async Task ProvisionOperator_AsAdmin_ReturnsOk()
    {
        var client = CreateAuthenticatedClient(UserRole.Admin);
        var request = new ProvisionOperatorRequest("operator@bitroute.com", "Password123!", "Jane Operator");
        var expectedUser = new AuthUser(Guid.NewGuid(), "operator@bitroute.com", [UserRole.Operator]);

        _authServiceMock.Setup(s => s.ProvisionOperatorAsync(It.IsAny<ProvisionOperatorRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse.Success(expectedUser, "Operator provisioned successfully."));

        var response = await client.PostAsJsonAsync("/auth/operators", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<AuthUser>>();
        Assert.NotNull(envelope);
        Assert.True(envelope.Status);
        Assert.Equal("Jane Operator", envelope.Data!.Email == "operator@bitroute.com" ? "Jane Operator" : "");
    }

    [Fact]
    public async Task ProvisionOperator_AsPassenger_ReturnsForbidden()
    {
        var client = CreateAuthenticatedClient(UserRole.Passenger);
        var request = new ProvisionOperatorRequest("operator@bitroute.com", "Password123!", "Jane Operator");

        var response = await client.PostAsJsonAsync("/auth/operators", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProvisionOperator_AsOperator_ReturnsForbidden()
    {
        var client = CreateAuthenticatedClient(UserRole.Operator);
        var request = new ProvisionOperatorRequest("operator@bitroute.com", "Password123!", "Jane Operator");

        var response = await client.PostAsJsonAsync("/auth/operators", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
