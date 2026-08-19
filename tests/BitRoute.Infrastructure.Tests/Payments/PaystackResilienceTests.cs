using System.Net;
using BitRoute.Domain.Interfaces;
using BitRoute.Infrastructure.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace BitRoute.Infrastructure.Tests.Payments;

public class PaystackResilienceTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        private readonly HttpStatusCode _statusCode;

        public MockHttpMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }

    [Fact]
    public async Task PaystackHttpClient_WithResilienceHandler_RetriesOnTransientErrors()
    {
        var services = new ServiceCollection();
        var handler = new MockHttpMessageHandler(HttpStatusCode.ServiceUnavailable);

        services.AddOptions<PaystackOptions>().Configure(o =>
        {
            o.SecretKey = "sk_test_12345";
        });
        services.AddLogging();
        services.AddHttpClient<IPaystackService, PaystackService>()
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddStandardResilienceHandler();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IPaystackService>();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.InitializeTransactionAsync("user@example.com", 5000, "ref123"));

        // Standard resilience handler retries transient failures (e.g. 503)
        Assert.True(handler.CallCount > 1, $"Expected retries, but handler was called {handler.CallCount} times.");
    }
}
