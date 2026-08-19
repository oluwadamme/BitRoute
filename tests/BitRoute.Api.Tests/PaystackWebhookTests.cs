using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using BitRoute.Application;
using BitRoute.Domain.Entities;
using BitRoute.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace BitRoute.Api.Tests;

[Collection("IntegrationTests")]
public class PaystackWebhookTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _dbFile;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IBookingRepository> _bookingRepositoryMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private const string TestSecretKey = "sk_test_mock_paystack_secret_key";

    public PaystackWebhookTests(WebApplicationFactory<Program> factory)
    {
        _dbFile = $"test_webhook_{Guid.NewGuid():N}.db";

        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", $"Data Source={_dbFile}");
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "localhost:6379");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "bitroute");
        Environment.SetEnvironmentVariable("Jwt__Audience", "bitroute-clients");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "test-signing-key-that-is-long-enough-123456");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "60");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenDays", "30");
        Environment.SetEnvironmentVariable("Paystack__SecretKey", TestSecretKey);

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IConnectionMultiplexer>();
                services.AddSingleton<IConnectionMultiplexer>(_redisMock.Object);

                services.RemoveAll<IBookingRepository>();
                services.AddScoped<IBookingRepository>(_ => _bookingRepositoryMock.Object);
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

    private static string ComputeHmacSha512(string payload, string key)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hash);
    }

    [Fact]
    public async Task HandleWebhook_InvalidSignature_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var payload = "{\"event\":\"charge.success\",\"data\":{\"reference\":\"ref123\",\"amount\":1000,\"status\":\"success\"}}";

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/paystack")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "invalid-signature");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HandleWebhook_ValidSignature_ProcessesPaymentConfirmation()
    {
        var client = _factory.CreateClient();
        var bookingId = Guid.NewGuid();

        var booking = SeatBooking.CreateHeld(
            Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Guid.NewGuid(), 0, 2, 2000, "key1");

        _bookingRepositoryMock.Setup(r => r.GetByIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var payload = $"{{\"event\":\"charge.success\",\"data\":{{\"reference\":\"{bookingId}\",\"amount\":2000,\"status\":\"success\"}}}}";
        var signature = ComputeHmacSha512(payload, TestSecretKey);

        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/paystack")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", signature);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(envelope);
        Assert.True(envelope.Status);
    }
}
