using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using BitRoute.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BitRoute.Infrastructure.Payments;

public sealed class PaystackService : IPaystackService
{
    private readonly HttpClient _httpClient;
    private readonly PaystackOptions _options;
    private readonly ILogger<PaystackService> _logger;

    public PaystackService(
        HttpClient httpClient,
        IOptions<PaystackOptions> options,
        ILogger<PaystackService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PaystackInitializeResponse> InitializeTransactionAsync(
        string email,
        int amountInKobo,
        string reference,
        CancellationToken cancellationToken = default)
    {
        var callbackUrl = _options.CallbackUrl;

        // If testing with mock keys, return a mock authorization URL immediately
        if (_options.SecretKey.StartsWith("sk_test_mock"))
        {
            _logger.LogInformation("Simulating Paystack checkout for reference '{Reference}' ({Amount} kobo).", reference, amountInKobo);
            var mockUrl = $"{callbackUrl}?reference={reference}&status=success";
            return new PaystackInitializeResponse(mockUrl, $"access_code_{reference}", reference);
        }

        var requestBody = new
        {
            email,
            amount = amountInKobo,
            reference,
            callback_url = callbackUrl
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.paystack.co/transaction/initialize")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.SecretKey);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PaystackInitApiResponse>(cancellationToken: cancellationToken);
        _logger.LogInformation("Paystack transaction initialized: {Result}", result);
        if (result is null || result?.Status != true || result.Data is null)
        {
            _logger.LogError("Failed to initialize Paystack transaction.");
            throw new InvalidOperationException("Failed to initialize Paystack transaction.");
        }

        return new PaystackInitializeResponse(
            result.Data.AuthorizationUrl,
            result.Data.AccessCode,
            result.Data.Reference);
    }

    public bool VerifyWebhookSignature(string payload, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(payload))
            return false;

        var keyBytes = Encoding.UTF8.GetBytes(_options.SecretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA512(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var computedBytes = Encoding.UTF8.GetBytes(Convert.ToHexStringLower(hash));
        var headerBytes = Encoding.UTF8.GetBytes(signatureHeader.ToLowerInvariant());

        if (computedBytes.Length != headerBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(computedBytes, headerBytes);
    }

    private sealed record PaystackInitApiResponse(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("data")] PaystackInitData Data);

    private sealed record PaystackInitData(
        [property: JsonPropertyName("authorization_url")] string AuthorizationUrl,
        [property: JsonPropertyName("access_code")] string AccessCode,
        [property: JsonPropertyName("reference")] string Reference);
}
