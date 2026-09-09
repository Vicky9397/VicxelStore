using Dpm.BuildingBlocks.Domain;
using Dpm.Orders.Domain;
using FluentAssertions;

namespace Dpm.UnitTests.Orders;

public sealed class LicenseTests
{
    private readonly TestClock _clock = new();

    private License IssueLicense(int downloadLimit = 2)
    {
        var order = Order.Place(
            1,
            Guid.NewGuid(),
            "IN",
            [new OrderLineDraft(1, 2, 3, 7, Money.Create(900m, "INR").Value, 15m, 1, downloadLimit)],
            Money.Zero("INR"),
            Money.Zero("INR"),
            _clock).Value;
        order.MarkPaid("INV-1", TimeSpan.FromDays(7), _clock);
        return order.Licenses[0];
    }

    [Fact]
    public void Each_download_spends_one_from_the_quota()
    {
        var license = IssueLicense(downloadLimit: 2);

        license.ConsumeDownload(_clock).IsSuccess.Should().BeTrue();
        license.DownloadsUsed.Should().Be(1);
        license.HasQuotaRemaining.Should().BeTrue();
    }

    [Fact]
    public void Downloads_are_refused_once_the_quota_is_spent()
    {
        var license = IssueLicense(downloadLimit: 1);
        license.ConsumeDownload(_clock);

        var exhausted = license.ConsumeDownload(_clock);

        exhausted.IsFailure.Should().BeTrue();
        exhausted.Error.Code.Should().Be("DOWNLOAD_LIMIT");
        license.DownloadsUsed.Should().Be(1, "a refused download does not consume quota");
    }
}

public sealed class CartTests
{
    private readonly TestClock _clock = new();

    private static Money Inr(decimal amount) => Money.Create(amount, "INR").Value;

    [Fact]
    public void Adding_the_same_variant_twice_is_idempotent()
    {
        var cart = Cart.CreateFor(1, _clock);

        cart.AddItem(1, 10, Inr(900m), _clock);
        cart.AddItem(1, 10, Inr(900m), _clock);

        cart.Items.Should().ContainSingle("a digital product is bought once per license");
    }

    [Fact]
    public void Re_adding_a_variant_refreshes_its_price_snapshot()
    {
        var cart = Cart.CreateFor(1, _clock);
        cart.AddItem(1, 10, Inr(900m), _clock);

        cart.AddItem(1, 10, Inr(750m), _clock);

        cart.Items[0].PriceSnapshotAmount.Should().Be(750m);
    }

    [Fact]
    public void A_cart_cannot_mix_currencies()
    {
        var cart = Cart.CreateFor(1, _clock);
        cart.AddItem(1, 10, Inr(900m), _clock);

        var mixed = cart.AddItem(2, 11, Money.Create(30m, "USD").Value, _clock);

        mixed.IsFailure.Should().BeTrue();
        mixed.Error.Code.Should().Be("CURRENCY_MISMATCH");
    }

    [Fact]
    public void Saved_for_later_items_are_not_payable()
    {
        var cart = Cart.CreateFor(1, _clock);
        cart.AddItem(1, 10, Inr(900m), _clock);
        cart.AddItem(2, 11, Inr(500m), _clock);

        cart.SaveForLater(11, saved: true, _clock);

        cart.PayableItems.Should().ContainSingle();
        cart.Items.Should().HaveCount(2);
    }

    [Fact]
    public void Clearing_the_cart_keeps_saved_for_later_items()
    {
        var cart = Cart.CreateFor(1, _clock);
        cart.AddItem(1, 10, Inr(900m), _clock);
        cart.AddItem(2, 11, Inr(500m), _clock);
        cart.SaveForLater(11, saved: true, _clock);

        cart.Clear(_clock);

        cart.Items.Should().ContainSingle();
        cart.Items[0].VariantId.Should().Be(11);
    }

    [Fact]
    public void Removing_an_item_that_is_not_there_reports_not_found()
    {
        var cart = Cart.CreateFor(1, _clock);

        cart.RemoveItem(99, _clock).IsFailure.Should().BeTrue();
    }
}
