using Dpm.BuildingBlocks.Domain;
using Dpm.Payments.Domain;

namespace Dpm.Payments.Application.Abstractions;

/// <summary>
/// Every gateway sits behind this interface (spec 03 section 3.1). The
/// application never references Stripe, PayPal or Razorpay directly, so adding
/// or swapping a provider touches only its adapter.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>"stripe" | "paypal" | "razorpay" | "manual_bank" | "sandbox".</summary>
    string Name { get; }

    Task<PaymentIntentResult> CreateIntentAsync(CreateIntent request, CancellationToken ct);

    Task<CaptureResult> CaptureAsync(string providerIntentId, Money amount, CancellationToken ct);

    Task<RefundResult> RefundAsync(string providerChargeId, Money amount, string reason, CancellationToken ct);

    /// <summary>
    /// Verifies the signature and normalizes the provider's payload into the
    /// canonical event vocabulary. An unverifiable payload must never parse into
    /// a usable event.
    /// </summary>
    WebhookParseResult ParseWebhook(string rawBody, IReadOnlyDictionary<string, string> headers);

    /// <summary>The processing fee this provider charges on an amount.</summary>
    Money CalculateFee(Money amount);
}

public sealed record CreateIntent(
    Guid OrderPublicId,
    Money Amount,
    string? ReturnUrl);

public sealed record PaymentIntentResult(
    string ProviderIntentId,
    string ClientSecret,
    string? Instructions);

public sealed record CaptureResult(bool Succeeded, string? ProviderChargeId, string? FailureReason);

public sealed record RefundResult(bool Succeeded, string? ProviderRefundId, string? FailureReason);

/// <param name="IsVerified">
/// False when the signature does not check out. The caller must discard the
/// payload entirely rather than trusting any field on it.
/// </param>
public sealed record WebhookParseResult(
    bool IsVerified,
    string? ProviderEventId,
    WebhookEventType Type,
    string? ProviderIntentId,
    string? ProviderChargeId,
    decimal? Amount,
    string? Currency,
    string? FailureReason);

/// <summary>Resolves a provider adapter by name (spec 03 section 3.1 factory).</summary>
public interface IPaymentProviderFactory
{
    IPaymentProvider Resolve(string providerName);

    bool IsSupported(string providerName);

    IReadOnlyList<string> SupportedProviders { get; }
}
