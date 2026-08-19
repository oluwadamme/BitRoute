using System.Text.Json;
using System.Text.Json.Serialization;
using BitRoute.Application;
using BitRoute.Application.Booking;
using BitRoute.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BitRoute.Api.Controllers;

[ApiController]
[Route("webhooks/paystack")]
public sealed class PaystackWebhookController : ControllerBase
{
    private readonly IPaystackService _paystackService;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPublisher _publisher;
    private readonly ILogger<PaystackWebhookController> _logger;

    private static readonly HashSet<string> ProcessedReferences = new();
    private static readonly object ProcessedLock = new();

    public PaystackWebhookController(
        IPaystackService paystackService,
        IBookingRepository bookingRepository,
        IPublisher publisher,
        ILogger<PaystackWebhookController> logger)
    {
        _paystackService = paystackService;
        _bookingRepository = bookingRepository;
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>Handles signature-verified callbacks from Paystack.</summary>
    [HttpPost]
    public async Task<IActionResult> HandleWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        var signatureHeader = Request.Headers["x-paystack-signature"].ToString();
        if (!_paystackService.VerifyWebhookSignature(payload, signatureHeader))
        {
            _logger.LogWarning("Invalid Paystack webhook signature header received.");
            return BadRequest(ApiResponse.Error("Invalid Paystack signature."));
        }

        var webhookEvent = JsonSerializer.Deserialize<PaystackWebhookEvent>(payload);
        if (webhookEvent == null || webhookEvent.Event != "charge.success")
        {
            _logger.LogInformation("Ignored Paystack event '{Event}'.", webhookEvent?.Event);
            return Ok();
        }

        var reference = webhookEvent.Data.Reference;
        var amount = webhookEvent.Data.Amount;

        lock (ProcessedLock)
        {
            if (ProcessedReferences.Contains(reference))
            {
                _logger.LogInformation("Duplicate Paystack webhook event ignored for reference '{Reference}'.", reference);
                return Ok(ApiResponse.SuccessMessage("Duplicate event processed."));
            }
            ProcessedReferences.Add(reference);
        }

        // Try to parse booking ID from reference or metadata
        if (Guid.TryParse(reference, out var bookingId))
        {
            await _publisher.Publish(new PaymentConfirmedNotification(bookingId, reference, amount), cancellationToken);
            _logger.LogInformation("Payment confirmed notification published for booking '{BookingId}'.", bookingId);
        }
        else
        {
            _logger.LogWarning("Could not parse booking ID from Paystack reference '{Reference}'.", reference);
        }

        return Ok(ApiResponse.SuccessMessage("Paystack webhook processed successfully."));
    }

    private sealed record PaystackWebhookEvent(
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("data")] PaystackWebhookData Data);

    private sealed record PaystackWebhookData(
        [property: JsonPropertyName("reference")] string Reference,
        [property: JsonPropertyName("amount")] int Amount,
        [property: JsonPropertyName("status")] string Status);
}
