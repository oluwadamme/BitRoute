namespace BitRoute.Infrastructure.Payments;

public sealed class PaystackOptions
{
    public const string SectionName = "Paystack";
    public required string SecretKey { get; set; }
    public required string PublicKey { get; set; }
    public required string CallbackUrl { get; set; }
}
