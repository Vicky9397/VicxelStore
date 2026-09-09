using Dpm.BuildingBlocks.Domain;
using Dpm.Orders.Domain;
using Dpm.Orders.Domain.Events;
using FluentAssertions;

namespace Dpm.UnitTests.Orders;

public sealed class OrderTests
{
    private readonly TestClock _clock = new();
    private readonly Guid _buyerPublicId = Guid.NewGuid();

    private static Money Inr(decimal amount) => Money.Create(amount, "INR").Value;

    private static OrderLineDraft Draft(decimal price = 1000m, decimal commission = 15m, long store = 7) =>
        new(ProductId: 1, VariantId: 2, VersionId: 3, StoreId: store,
            Inr(price), commission, LicenseType: 1, DownloadLimit: 5);

    private Order PlaceOrder(params OrderLineDraft[] lines) =>
        Order.Place(1, _buyerPublicId, "IN", lines, Inr(180m), Inr(0m), _clock).Value;

    [Fact]
    public void A_placed_order_starts_pending_with_no_licenses()
    {
        var order = PlaceOrder(Draft());

        order.Status.Should().Be(OrderStatus.Pending);
        order.Licenses.Should().BeEmpty("licenses follow the capture, never the order");
        order.IsPaid.Should().BeFalse();
        order.DomainEvents.OfType<OrderPlaced>().Should().ContainSingle();
    }

    [Fact]
    public void Totals_follow_the_specification_identity()
    {
        var order = PlaceOrder(Draft(1000m), Draft(500m));

        order.Subtotal.Should().Be(1500m);
        order.TaxTotal.Should().Be(180m);
        order.GrandTotal.Should().Be(1680m, "subtotal minus discount plus tax");
        order.TotalsBalance.Should().BeTrue();
    }

    [Fact]
    public void An_empty_order_is_refused()
    {
        var placed = Order.Place(1, _buyerPublicId, "IN", [], Inr(0m), Inr(0m), _clock);

        placed.IsFailure.Should().BeTrue();
        placed.Error.Code.Should().Be("CART_EMPTY");
    }

    [Fact]
    public void An_order_mixing_currencies_is_refused()
    {
        var mixed = new OrderLineDraft(1, 2, 3, 7, Money.Create(30m, "USD").Value, 15m, 1, 5);

        var placed = Order.Place(1, _buyerPublicId, "IN", [Draft(), mixed], Inr(0m), Inr(0m), _clock);

        placed.IsFailure.Should().BeTrue();
        placed.Error.Code.Should().Be("CURRENCY_MISMATCH");
    }

    [Fact]
    public void A_discount_larger_than_the_order_is_refused()
    {
        var placed = Order.Place(
            1, _buyerPublicId, "IN", [Draft(100m)], Inr(0m), Inr(500m), _clock);

        placed.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Capture_issues_one_license_per_line_and_starts_the_hold_clock()
    {
        var order = PlaceOrder(Draft(), Draft(500m));
        order.ClearDomainEvents();

        var paid = order.MarkPaid("INV-2026-00000001", TimeSpan.FromDays(7), _clock);

        paid.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Paid);
        order.Licenses.Should().HaveCount(2);
        order.InvoiceNo.Should().Be("INV-2026-00000001");
        order.HoldReleaseAtUtc.Should().Be(_clock.UtcNow.AddDays(7));
        order.DomainEvents.OfType<LicenseIssued>().Should().HaveCount(2);
        order.DomainEvents.OfType<InvoiceGenerated>().Should().ContainSingle();
    }

    [Fact]
    public void A_replayed_capture_does_not_issue_a_second_set_of_licenses()
    {
        var order = PlaceOrder(Draft());
        order.MarkPaid("INV-1", TimeSpan.FromDays(7), _clock);
        order.ClearDomainEvents();

        var replay = order.MarkPaid("INV-2", TimeSpan.FromDays(7), _clock);

        replay.IsSuccess.Should().BeTrue("an at-least-once webhook must be safe to redeliver");
        order.Licenses.Should().HaveCount(1);
        order.InvoiceNo.Should().Be("INV-1", "the first invoice number stands");
        order.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void A_cancelled_order_cannot_later_be_marked_paid()
    {
        var order = PlaceOrder(Draft());
        order.Cancel("Payment declined.", _clock);

        order.MarkPaid("INV-1", TimeSpan.FromDays(7), _clock).IsFailure.Should().BeTrue();
        order.Licenses.Should().BeEmpty();
    }

    [Fact]
    public void A_paid_order_cannot_be_cancelled()
    {
        var order = PlaceOrder(Draft());
        order.MarkPaid("INV-1", TimeSpan.FromDays(7), _clock);

        order.Cancel("Changed my mind.", _clock).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_line_splits_its_price_into_commission_and_seller_earning()
    {
        var order = PlaceOrder(Draft(1000m, commission: 15m));
        var line = order.Lines[0];

        line.Commission.Amount.Should().Be(150m);
        line.SellerEarning.Amount.Should().Be(850m);
        line.Commission.Add(line.SellerEarning).Should().Be(line.UnitPrice);
    }

    [Fact]
    public void The_licenses_quota_comes_from_what_the_buyer_paid_for()
    {
        var order = PlaceOrder(new OrderLineDraft(1, 2, 3, 7, Inr(1000m), 15m, 1, DownloadLimit: 12));
        order.MarkPaid("INV-1", TimeSpan.FromDays(7), _clock);

        order.Licenses[0].DownloadLimit.Should().Be(12);
        order.Licenses[0].DownloadsUsed.Should().Be(0);
    }
}
