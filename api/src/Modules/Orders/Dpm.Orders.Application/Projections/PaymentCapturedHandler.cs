using Dpm.BuildingBlocks.Domain;
using Dpm.BuildingBlocks.IntegrationEvents;
using Dpm.Orders.Application.Abstractions;
using MediatR;

namespace Dpm.Orders.Application.Projections;

/// <summary>
/// Completes the purchase once Payments confirms the capture: the order moves to
/// Paid, one license is issued per line, and the payout hold clock starts.
///
/// Delivery is at-least-once, so this must be replay-safe. It is: an order
/// already Paid returns success without issuing a second set of licenses.
/// </summary>
public sealed class PaymentCapturedHandler(
    IOrderRepository orders,
    IOrdersUnitOfWork unitOfWork,
    IInvoiceNumbers invoiceNumbers,
    IOrderPolicy orderPolicy,
    IClock clock)
    : INotificationHandler<PaymentCaptured>
{
    public async Task Handle(PaymentCaptured notification, CancellationToken cancellationToken)
    {
        var order = await orders.FindByIdAsync(notification.OrderId, cancellationToken);
        if (order is null || order.IsPaid)
        {
            return;
        }

        var holdPeriod = await orderPolicy.GetPayoutHoldPeriodAsync(cancellationToken);
        var invoiceNo = await invoiceNumbers.NextAsync(cancellationToken);

        var paid = order.MarkPaid(invoiceNo, holdPeriod, clock);
        if (paid.IsFailure)
        {
            // Surfaced to the dispatcher, which retries and eventually parks the
            // message so the break is visible rather than silent.
            throw new InvalidOperationException(
                $"Order {order.PublicId} could not be marked paid: {paid.Error.Message}");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
