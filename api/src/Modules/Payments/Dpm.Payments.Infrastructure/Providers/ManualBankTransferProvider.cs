using System.Globalization;
using Dpm.BuildingBlocks.Domain;
using Dpm.Payments.Application.Abstractions;
using Dpm.Payments.Domain;
using Microsoft.Extensions.Options;

namespace Dpm.Payments.Infrastructure.Providers;

/// <summary>
/// Manual bank transfer (spec 03 section 3.1). The intent returns bank details
/// and a reference; an administrator confirms receipt, which emits the same
/// canonical PaymentSucceeded every other provider emits. There is no webhook
/// from a bank, so this adapter parses nothing.
/// </summary>
public sealed class ManualBankTransferProvider(IOptions<PaymentProviderOptions> options) : IPaymentProvider
{
    private readonly PaymentProviderOptions _options = options.Value;

    public string Name => "manual_bank";

    public Task<PaymentIntentResult> CreateIntentAsync(CreateIntent request, CancellationToken ct)
    {
        var reference = $"MB-{request.OrderPublicId.ToString("N")[..10].ToUpperInvariant()}";
        var bank = _options.ManualBank;
        var instructions =
            $"Transfer {request.Amount.Amount.ToString("0.00", CultureInfo.InvariantCulture)} " +
            $"{request.Amount.Currency} to {bank.AccountName}, account {bank.AccountNumberMasked}, " +
            $"IFSC {bank.Ifsc}. Quote reference {reference}. " +
            "Your order completes once an administrator confirms the transfer.";

        return Task.FromResult(new PaymentIntentResult(reference, reference, instructions));
    }

    public Task<CaptureResult> CaptureAsync(string providerIntentId, Money amount, CancellationToken ct) =>
        Task.FromResult(new CaptureResult(true, providerIntentId, null));

    /// <summary>Refunds are arranged out of band and recorded by an administrator.</summary>
    public Task<RefundResult> RefundAsync(
        string providerChargeId,
        Money amount,
        string reason,
        CancellationToken ct) =>
        Task.FromResult(new RefundResult(false, null, "Manual bank refunds are issued outside the platform."));

    /// <summary>No gateway fee applies to a direct transfer.</summary>
    public Money CalculateFee(Money amount) => Money.Zero(amount.Currency);

    /// <summary>A bank does not call back, so nothing here is ever verifiable.</summary>
    public WebhookParseResult ParseWebhook(string rawBody, IReadOnlyDictionary<string, string> headers) =>
        new(false, null, WebhookEventType.Unknown, null, null, null, null, null);
}
