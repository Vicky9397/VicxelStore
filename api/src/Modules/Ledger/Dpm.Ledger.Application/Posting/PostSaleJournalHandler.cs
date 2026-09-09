using Dpm.BuildingBlocks.Domain;
using Dpm.BuildingBlocks.IntegrationEvents;
using Dpm.Ledger.Application.Abstractions;
using Dpm.Ledger.Domain;
using Dpm.Orders.Contracts;
using MediatR;

namespace Dpm.Ledger.Application.Posting;

/// <summary>
/// Posts the sale journal when Payments confirms a capture. This is the step
/// that makes the money real in the books, so it is written to be safe under the
/// outbox's at-least-once delivery: a transaction already posted for this order
/// short-circuits rather than double-crediting the seller
/// (spec 03 section 3.6).
///
/// A failure here throws, which leaves the outbox message unprocessed and
/// retried. The spec's rule is that a capture is never left without a ledger
/// row, so failing loudly is correct and swallowing the error would not be.
/// </summary>
public sealed class PostSaleJournalHandler(
    ILedgerRepository ledger,
    ILedgerUnitOfWork unitOfWork,
    IOrderDirectory orders,
    IClock clock)
    : INotificationHandler<PaymentCaptured>
{
    public async Task Handle(PaymentCaptured notification, CancellationToken cancellationToken)
    {
        if (await ledger.ExistsForReferenceAsync(
                LedgerRefType.Order, notification.OrderId, cancellationToken))
        {
            return;
        }

        var order = await orders.FindOrderAsync(notification.OrderId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Captured payment references order {notification.OrderId}, which does not exist.");

        var currency = order.Currency;
        var saleLines = order.Lines
            .Select(l => new SaleLine(
                l.StoreId,
                Money.Create(l.UnitAmount, currency).Value,
                l.CommissionPct))
            .ToList();

        var journal = SaleJournal.Build(
            saleLines,
            Money.Create(order.TaxTotal, currency).Value,
            Money.Create(notification.GatewayFeeAmount, currency).Value,
            Money.Create(order.GrandTotal, currency).Value);

        if (journal.IsFailure)
        {
            throw new InvalidOperationException(
                $"Sale journal for order {order.PublicId} could not be built: {journal.Error.Message}");
        }

        var posted = LedgerTransaction.Post(
            LedgerRefType.Order,
            order.OrderId,
            $"Sale {order.PublicId}",
            journal.Value,
            clock);

        if (posted.IsFailure)
        {
            throw new InvalidOperationException(
                $"Sale journal for order {order.PublicId} did not balance: {posted.Error.Message}");
        }

        ledger.Add(posted.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
