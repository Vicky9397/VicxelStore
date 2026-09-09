using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dpm.BuildingBlocks.Domain;
using Dpm.Payments.Application.Abstractions;
using Dpm.Payments.Domain;
using Microsoft.Extensions.Options;

namespace Dpm.Payments.Infrastructure.Providers;

/// <summary>
/// A local provider that implements the full contract, including HMAC-signed
/// webhooks, so the capture path, signature verification, inbox deduplication
/// and ledger posting are all exercised without a network call.
///
/// It is not a payment processor. A hosted gateway adapter (Stripe or Razorpay)
/// implements this same interface and replaces it by configuration; nothing
/// outside this file changes when it does.
/// </summary>
public sealed class SandboxPaymentProvider(IOptions<PaymentProviderOptions> options) : IPaymentProvider
{
    private readonly PaymentProviderOptions _options = options.Value;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string Name => "sandbox";

    public Task<PaymentIntentResult> CreateIntentAsync(CreateIntent request, CancellationToken ct)
    {
        var intentId = $"sbx_{Guid.NewGuid():N}";
        // The secret is the client's proof it may complete this one charge; it
        // carries no authority beyond that.
        var clientSecret = $"{intentId}_secret_{Guid.NewGuid():N}";
        return Task.FromResult(new PaymentIntentResult(
            intentId,
            clientSecret,
            "Sandbox provider: confirm the payment from the checkout page to trigger a signed webhook."));
    }

    public Task<CaptureResult> CaptureAsync(string providerIntentId, Money amount, CancellationToken ct) =>
        Task.FromResult(new CaptureResult(true, $"sbx_ch_{Guid.NewGuid():N}", null));

    public Task<RefundResult> RefundAsync(
        string providerChargeId,
        Money amount,
        string reason,
        CancellationToken ct) =>
        Task.FromResult(new RefundResult(true, $"sbx_rf_{Guid.NewGuid():N}", null));

    public Money CalculateFee(Money amount) => amount.MultiplyPercent(_options.SandboxFeePct);

    public WebhookParseResult ParseWebhook(string rawBody, IReadOnlyDictionary<string, string> headers)
    {
        if (!headers.TryGetValue("X-Sandbox-Signature", out var signature)
            || !VerifySignature(rawBody, signature))
        {
            return Unverified();
        }

        SandboxWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SandboxWebhookPayload>(rawBody, SerializerOptions);
        }
        catch (JsonException)
        {
            return Unverified();
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.EventId))
        {
            return Unverified();
        }

        var type = payload.Type switch
        {
            "payment.succeeded" => WebhookEventType.PaymentSucceeded,
            "payment.failed" => WebhookEventType.PaymentFailed,
            "charge.refunded" => WebhookEventType.ChargeRefunded,
            _ => WebhookEventType.Unknown,
        };

        return new WebhookParseResult(
            IsVerified: true,
            payload.EventId,
            type,
            payload.IntentId,
            payload.ChargeId,
            payload.Amount,
            payload.Currency,
            payload.FailureReason);
    }

    /// <summary>Signs a payload the way the provider would, for tests and the dev confirm endpoint.</summary>
    public string Sign(string rawBody) =>
        Convert.ToHexString(HMACSHA256.HashData(SecretBytes(), Encoding.UTF8.GetBytes(rawBody)))
            .ToLowerInvariant();

    private bool VerifySignature(string rawBody, string presented)
    {
        var expected = Sign(rawBody);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var presentedBytes = Encoding.UTF8.GetBytes(presented.Trim().ToLowerInvariant());
        return expectedBytes.Length == presentedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, presentedBytes);
    }

    private byte[] SecretBytes() => Encoding.UTF8.GetBytes(
        string.IsNullOrWhiteSpace(_options.SandboxWebhookSecret)
            ? "sandbox-development-secret"
            : _options.SandboxWebhookSecret);

    private static WebhookParseResult Unverified() =>
        new(false, null, WebhookEventType.Unknown, null, null, null, null, null);

    /// <summary>Builds a succeeded-payment payload, used by the development confirm endpoint and tests.</summary>
    public static string BuildSucceededPayload(string intentId, decimal amount, string currency) =>
        JsonSerializer.Serialize(
            new SandboxWebhookPayload(
                $"evt_{Guid.NewGuid():N}",
                "payment.succeeded",
                intentId,
                $"sbx_ch_{Guid.NewGuid():N}",
                amount,
                currency,
                null),
            SerializerOptions);

    public static string BuildFailedPayload(string intentId, string reason) =>
        JsonSerializer.Serialize(
            new SandboxWebhookPayload(
                $"evt_{Guid.NewGuid():N}",
                "payment.failed",
                intentId,
                null,
                null,
                null,
                reason),
            SerializerOptions);

    public sealed record SandboxWebhookPayload(
        string EventId,
        string Type,
        string? IntentId,
        string? ChargeId,
        decimal? Amount,
        string? Currency,
        string? FailureReason);
}
