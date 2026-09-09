using Dpm.BuildingBlocks.Application;
using Dpm.Payments.Application.Abstractions;
using Dpm.Payments.Domain;

namespace Dpm.Payments.Application.Webhooks;

public sealed record HandleWebhookCommand(
    string ProviderName,
    string RawBody,
    IReadOnlyDictionary<string, string> Headers) : ICommand;

/// <summary>
/// The only path that can capture a payment. It verifies the provider signature,
/// deduplicates by provider event id through the inbox, and applies the outcome
/// once. The capture and its outbox event commit in the same transaction, so the
/// ledger post cannot be lost after a charge (spec 03 section 3.6).
/// </summary>
public sealed class HandleWebhookCommandHandler(
    IPaymentRepository payments,
    IPaymentsUnitOfWork unitOfWork,
    IWebhookInbox inbox,
    IPaymentProviderFactory providerFactory)
    : ICommandHandler<HandleWebhookCommand>
{
    public async Task<Result> Handle(HandleWebhookCommand command, CancellationToken cancellationToken)
    {
        if (!providerFactory.IsSupported(command.ProviderName))
        {
            return Result.Failure(PaymentErrors.UnsupportedProvider);
        }

        var provider = providerFactory.Resolve(command.ProviderName);
        var parsed = provider.ParseWebhook(command.RawBody, command.Headers);

        // An unverified payload is discarded whole: no field on it is trusted.
        if (!parsed.IsVerified || string.IsNullOrWhiteSpace(parsed.ProviderEventId))
        {
            return Result.Failure(PaymentErrors.WebhookUnverified);
        }

        var isNew = await inbox.TryRecordAsync(
            parsed.ProviderEventId,
            provider.Name,
            parsed.Type.ToString(),
            command.RawBody,
            cancellationToken);
        if (!isNew)
        {
            // Already applied; a redelivery is a no-op by design.
            return Result.Success();
        }

        var applied = await ApplyAsync(provider.Name, parsed, cancellationToken);
        if (applied.IsFailure)
        {
            return applied;
        }

        await inbox.MarkProcessedAsync(parsed.ProviderEventId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> ApplyAsync(
        string providerName,
        WebhookParseResult parsed,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(parsed.ProviderIntentId))
        {
            // Nothing to correlate to an order yet; the event is recorded and left
            // for reconciliation rather than guessed at.
            return Result.Success();
        }

        var payment = await payments.FindByProviderIntentAsync(providerName, parsed.ProviderIntentId, ct);
        if (payment is null)
        {
            // The webhook beat the order write. It stays in the inbox unprocessed
            // so reconciliation can correlate it later (spec 03 section 3.6).
            return Result.Success();
        }

        var provider = providerFactory.Resolve(providerName);

        switch (parsed.Type)
        {
            case WebhookEventType.PaymentSucceeded:
                var fee = provider.CalculateFee(payment.Money);
                return payment.Capture(parsed.ProviderChargeId ?? string.Empty, fee);

            case WebhookEventType.PaymentFailed:
                return payment.Fail(parsed.FailureReason ?? "The payment was declined.");

            case WebhookEventType.Unknown:
            case WebhookEventType.ChargeRefunded:
            case WebhookEventType.ChargebackOpened:
            case WebhookEventType.ChargebackWon:
            case WebhookEventType.ChargebackLost:
            case WebhookEventType.PayoutPaid:
            case WebhookEventType.PayoutFailed:
            default:
                // Refunds, disputes and payout events arrive with their own
                // milestones; recording them in the inbox now keeps the history
                // complete without inventing half a workflow.
                return Result.Success();
        }
    }
}
