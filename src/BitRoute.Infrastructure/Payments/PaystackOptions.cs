namespace BitRoute.Infrastructure.Payments;

public sealed class PaystackOptions
{
    public const string SectionName = "Paystack";

    public string SecretKey { get; set; } = "sk_test_mock_paystack_secret_key";
    public string PublicKey { get; set; } = "pk_test_mock_paystack_public_key";
    public string CallbackUrl { get; set; } = "http://localhost:3001/payment/callback";
}
