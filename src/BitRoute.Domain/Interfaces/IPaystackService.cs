namespace BitRoute.Domain.Interfaces;

public record PaystackInitializeResponse(string AuthorizationUrl, string AccessCode, string Reference);

public interface IPaystackService
{
    Task<PaystackInitializeResponse> InitializeTransactionAsync(
        string email,
        int amountInKobo,
        string reference,
        string callbackUrl,
        CancellationToken cancellationToken = default);

    bool VerifyWebhookSignature(string payload, string signatureHeader);
}
