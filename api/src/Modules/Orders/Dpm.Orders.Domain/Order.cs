using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Orders.Domain.Events;

namespace Dpm.Orders.Domain;

/// <summary>
/// Order aggregate (orders.Orders). It owns its lines and the licenses they
/// grant, and enforces the money invariants from spec 02 ORD-01:
/// grand total = subtotal - discount + tax, one currency per order, and licenses
/// issued only once payment is captured.
/// </summary>
public sealed class Order : AggregateRoot
{
    private readonly List<OrderLine> _lines = [];
    private readonly List<License> _licenses = [];

    public long BuyerUserId { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal Subtotal { get; private set; }

    public decimal DiscountTotal { get; private set; }

    public decimal TaxTotal { get; private set; }

    public decimal GrandTotal { get; private set; }

    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    public string? InvoiceNo { get; private set; }

    public string? BillingCountry { get; private set; }

    public DateTime PlacedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime? HoldReleaseAtUtc { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    public IReadOnlyList<License> Licenses => _licenses.AsReadOnly();

    private Order()
    {
    }

    /// <summary>
    /// Builds a Pending order from priced lines. Nothing is charged and no
    /// license exists yet; both follow the capture.
    /// </summary>
    public static Result<Order> Place(
        long buyerUserId,
        Guid buyerPublicId,
        string billingCountry,
        IReadOnlyList<OrderLineDraft> lines,
        Money taxTotal,
        Money discountTotal,
        IClock clock)
    {
        if (lines.Count == 0)
        {
            return Result.Failure<Order>(Error.Conflict("CART_EMPTY", "There is nothing to check out."));
        }

        var currency = lines[0].UnitPrice.Currency;
        if (lines.Any(l => l.UnitPrice.Currency != currency))
        {
            return Result.Failure<Order>(Error.Conflict(
                "CURRENCY_MISMATCH",
                "All items in one order must share a single currency."));
        }

        if (taxTotal.Currency != currency || discountTotal.Currency != currency)
        {
            return Result.Failure<Order>(Error.Conflict(
                "CURRENCY_MISMATCH",
                "Tax and discount must be in the order's currency."));
        }

        var subtotal = lines.Aggregate(
            Money.Zero(currency),
            (running, line) => running.Add(line.UnitPrice));

        var grandTotal = subtotal.Subtract(discountTotal).Add(taxTotal);
        if (grandTotal.IsNegative)
        {
            return Result.Failure<Order>(Error.Validation(
                "VALIDATION_ERROR",
                "Discount cannot exceed the order total."));
        }

        var order = new Order
        {
            PublicId = Guid.NewGuid(),
            BuyerUserId = buyerUserId,
            Currency = currency,
            Subtotal = subtotal.Amount,
            DiscountTotal = discountTotal.Amount,
            TaxTotal = taxTotal.Amount,
            GrandTotal = grandTotal.Amount,
            BillingCountry = billingCountry,
            PlacedAtUtc = clock.UtcNow,
        };

        foreach (var line in lines)
        {
            order._lines.Add(OrderLine.Create(
                line.ProductId,
                line.VariantId,
                line.VersionId,
                line.StoreId,
                line.UnitPrice,
                line.CommissionPct,
                line.LicenseType,
                line.DownloadLimit));
        }

        order.Raise(new OrderPlaced(order.PublicId, buyerPublicId, order.GrandTotal, currency));
        return Result.Success(order);
    }

    /// <summary>
    /// Applied when the payment provider confirms capture. Issues one license per
    /// line and starts the payout hold clock. Replaying a capture is a no-op, so
    /// a webhook delivered twice cannot double-issue licenses.
    /// </summary>
    public Result MarkPaid(string invoiceNo, TimeSpan holdPeriod, IClock clock)
    {
        if (Status == OrderStatus.Paid || Status == OrderStatus.Completed)
        {
            return Result.Success();
        }

        if (Status != OrderStatus.Pending)
        {
            return Result.Failure(Error.Conflict(
                "CONFLICT",
                $"An order in {Status} cannot be marked paid."));
        }

        Status = OrderStatus.Paid;
        CompletedAtUtc = clock.UtcNow;
        HoldReleaseAtUtc = clock.UtcNow.Add(holdPeriod);
        InvoiceNo = invoiceNo;

        foreach (var line in _lines)
        {
            var license = License.Issue(line, BuyerUserId, clock);
            _licenses.Add(license);
            Raise(new LicenseIssued(license.PublicId, PublicId, line.VariantId));
        }

        Raise(new InvoiceGenerated(PublicId, invoiceNo));
        return Result.Success();
    }

    public Result Cancel(string reason, IClock clock)
    {
        if (Status != OrderStatus.Pending)
        {
            return Result.Failure(Error.Conflict("CONFLICT", "Only a pending order can be cancelled."));
        }

        Status = OrderStatus.Cancelled;
        CompletedAtUtc = clock.UtcNow;
        Raise(new OrderCancelled(PublicId, reason));
        return Result.Success();
    }

    public bool IsPaid => Status is OrderStatus.Paid or OrderStatus.Completed;

    /// <summary>
    /// The accounting identity every order must satisfy. Asserted in tests and
    /// re-checked before the ledger posts, so a malformed order cannot become a
    /// journal entry.
    /// </summary>
    public bool TotalsBalance =>
        GrandTotal == Subtotal - DiscountTotal + TaxTotal
        && Subtotal == _lines.Sum(l => l.UnitAmount);
}

/// <param name="CommissionPct">
/// Resolved at checkout from the seller override, the category rate, or the
/// platform default, then frozen onto the line so a later rate change never
/// rewrites a settled sale (spec 03 section 3.4 commission engine).
/// </param>
public sealed record OrderLineDraft(
    long ProductId,
    long VariantId,
    long VersionId,
    long StoreId,
    Money UnitPrice,
    decimal CommissionPct,
    byte LicenseType,
    int DownloadLimit);
