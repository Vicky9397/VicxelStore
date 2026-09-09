using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.BuildingBlocks.IntegrationEvents;
using Dpm.Payments.Domain.Events;

namespace Dpm.Payments.Domain;

/// <summary>
/// A charge against one order (pay.Payments). Capture is driven only by a
/// verified provider webhook, never by the client, and replaying that webhook is
/// a no-op so a duplicate delivery cannot double-credit the ledger
/// (spec 03 section 3.6).
/// </summary>
public sealed class Payment : AggregateRoot
{
    public long OrderId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string ProviderIntentId { get; private set; } = string.Empty;

    public string? ProviderChargeId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal? FxRate { get; private set; }

    public string? SettlementCurrency { get; private set; }

    public PaymentStatus Status { get; private set; } = PaymentStatus.Created;

    public DateTime CreatedAtUtc { get; private set; }

    private Payment()
    {
    }

    public static Payment Create(
        long orderId,
        string provider,
        string providerIntentId,
        Money amount,
        IClock clock) => new()
        {
            PublicId = Guid.NewGuid(),
            OrderId = orderId,
            Provider = Guard.AgainstNullOrWhiteSpace(provider, nameof(provider)),
            ProviderIntentId = Guard.AgainstNullOrWhiteSpace(providerIntentId, nameof(providerIntentId)),
            Amount = amount.Amount,
            Currency = amount.Currency,
            CreatedAtUtc = clock.UtcNow,
        };

    /// <summary>
    /// Records a confirmed capture and announces it so Orders can issue licenses
    /// and Ledger can post the journal. Capturing an already-captured payment
    /// returns success without raising again.
    /// </summary>
    public Result Capture(string providerChargeId, Money gatewayFee)
    {
        if (Status == PaymentStatus.Captured)
        {
            return Result.Success();
        }

        if (Status != PaymentStatus.Created)
        {
            return Result.Failure(Error.Conflict(
                "CONFLICT",
                $"A payment in {Status} cannot be captured."));
        }

        if (gatewayFee.Currency != Currency)
        {
            return Result.Failure(Error.Conflict(
                "CURRENCY_MISMATCH",
                "The gateway fee must be in the payment's currency."));
        }

        Status = PaymentStatus.Captured;
        ProviderChargeId = providerChargeId;

        Raise(new PaymentCaptured(
            PublicId,
            OrderId,
            Amount,
            Currency,
            gatewayFee.Amount));

        return Result.Success();
    }

    public Result Fail(string reason)
    {
        if (Status == PaymentStatus.Failed)
        {
            return Result.Success();
        }

        if (Status != PaymentStatus.Created)
        {
            return Result.Failure(Error.Conflict(
                "CONFLICT",
                $"A payment in {Status} cannot be marked failed."));
        }

        Status = PaymentStatus.Failed;
        Raise(new PaymentFailedRecorded(PublicId, OrderId, reason));
        return Result.Success();
    }

    public Money Money => BuildingBlocks.Domain.Money.Create(Amount, Currency).Value;
}
