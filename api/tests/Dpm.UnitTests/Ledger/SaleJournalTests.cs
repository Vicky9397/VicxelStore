using Dpm.BuildingBlocks.Domain;
using Dpm.Ledger.Domain;
using FluentAssertions;

namespace Dpm.UnitTests.Ledger;

public sealed class SaleJournalTests
{
    private readonly TestClock _clock = new();

    private static Money Inr(decimal amount) => Money.Create(amount, "INR").Value;

    /// <summary>
    /// The worked example from spec 03 section 3.3, reproduced line for line.
    /// If this drifts, the money model has changed.
    /// </summary>
    [Fact]
    public void The_specification_worked_example_reproduces_exactly()
    {
        var lines = SaleJournal.Build(
            [new SaleLine(SellerStoreId: 7, Inr(1000m), CommissionPct: 15m)],
            taxTotal: Inr(180m),
            gatewayFee: Inr(25m),
            grandTotal: Inr(1180m));

        lines.IsSuccess.Should().BeTrue();

        decimal Debit(string account) => lines.Value.Where(l => l.Account == account).Sum(l => l.Debit);
        decimal Credit(string account) => lines.Value.Where(l => l.Account == account).Sum(l => l.Credit);

        Debit(LedgerAccount.CashGateway).Should().Be(1180.00m);
        Credit(LedgerAccount.CashGateway).Should().Be(25.00m);
        Credit(LedgerAccount.TaxPayable).Should().Be(180.00m);
        Credit(LedgerAccount.SellerPayablePending).Should().Be(850.00m);
        Credit(LedgerAccount.PlatformRevenue).Should().Be(150.00m);
        Debit(LedgerAccount.GatewayFees).Should().Be(25.00m);

        lines.Value.Sum(l => l.Debit).Should().Be(1205.00m);
        lines.Value.Sum(l => l.Credit).Should().Be(1205.00m);
    }

    [Fact]
    public void Every_sale_journal_balances_to_zero()
    {
        var lines = SaleJournal.Build(
            [new SaleLine(7, Inr(1000m), 15m)], Inr(180m), Inr(25m), Inr(1180m));

        var posted = LedgerTransaction.Post(
            LedgerRefType.Order, 1, "Sale", lines.Value, _clock);

        posted.IsSuccess.Should().BeTrue();
        posted.Value.IsBalanced.Should().BeTrue();
    }

    [Theory]
    [InlineData(1000, 15, 180, 25, 1180)]
    [InlineData(2400, 20, 432, 60, 2832)]
    [InlineData(99.99, 15, 18.00, 2.50, 117.99)]
    [InlineData(1, 15, 0.18, 0.03, 1.18)]
    public void Journals_balance_across_a_range_of_amounts_and_rates(
        decimal price, decimal commissionPct, decimal tax, decimal fee, decimal grandTotal)
    {
        var lines = SaleJournal.Build(
            [new SaleLine(7, Inr(price), commissionPct)], Inr(tax), Inr(fee), Inr(grandTotal));

        lines.IsSuccess.Should().BeTrue();
        lines.Value.Sum(l => l.Debit).Should().Be(lines.Value.Sum(l => l.Credit));
    }

    [Fact]
    public void A_multi_seller_basket_credits_each_seller_separately()
    {
        var lines = SaleJournal.Build(
            [
                new SaleLine(SellerStoreId: 7, Inr(1000m), 15m),
                new SaleLine(SellerStoreId: 9, Inr(500m), 20m),
            ],
            taxTotal: Inr(270m),
            gatewayFee: Inr(40m),
            grandTotal: Inr(1770m));

        lines.IsSuccess.Should().BeTrue();

        var sellerLines = lines.Value
            .Where(l => l.Account == LedgerAccount.SellerPayablePending)
            .ToList();

        sellerLines.Should().HaveCount(2);
        sellerLines.Single(l => l.SellerStoreId == 7).Credit.Should().Be(850.00m);
        sellerLines.Single(l => l.SellerStoreId == 9).Credit.Should().Be(400.00m);

        lines.Value.Where(l => l.Account == LedgerAccount.PlatformRevenue).Sum(l => l.Credit)
            .Should().Be(250.00m, "150 from the first seller and 100 from the second");
        lines.Value.Sum(l => l.Debit).Should().Be(lines.Value.Sum(l => l.Credit));
    }

    [Fact]
    public void A_zero_tax_sale_still_balances()
    {
        var lines = SaleJournal.Build(
            [new SaleLine(7, Inr(1000m), 15m)], Inr(0m), Inr(25m), Inr(1000m));

        lines.IsSuccess.Should().BeTrue();
        lines.Value.Should().NotContain(l => l.Account == LedgerAccount.TaxPayable);
        lines.Value.Sum(l => l.Debit).Should().Be(lines.Value.Sum(l => l.Credit));
    }

    [Fact]
    public void A_zero_fee_provider_posts_no_gateway_expense()
    {
        var lines = SaleJournal.Build(
            [new SaleLine(7, Inr(1000m), 15m)], Inr(180m), Inr(0m), Inr(1180m));

        lines.Value.Should().NotContain(l => l.Account == LedgerAccount.GatewayFees);
        lines.Value.Sum(l => l.Debit).Should().Be(lines.Value.Sum(l => l.Credit));
    }

    [Fact]
    public void A_journal_mixing_currencies_is_refused()
    {
        var lines = SaleJournal.Build(
            [new SaleLine(7, Money.Create(1000m, "USD").Value, 15m)],
            Inr(180m),
            Inr(25m),
            Inr(1180m));

        lines.IsFailure.Should().BeTrue();
        lines.Error.Code.Should().Be("LEDGER_CURRENCY_MISMATCH");
    }

    [Fact]
    public void A_hold_release_moves_pending_to_available_and_balances()
    {
        var lines = SaleJournal.BuildHoldRelease(sellerStoreId: 7, Inr(850m));

        lines.IsSuccess.Should().BeTrue();
        lines.Value.Single(l => l.Account == LedgerAccount.SellerPayablePending).Debit.Should().Be(850m);
        lines.Value.Single(l => l.Account == LedgerAccount.SellerPayableAvailable).Credit.Should().Be(850m);
        lines.Value.Sum(l => l.Debit).Should().Be(lines.Value.Sum(l => l.Credit));
    }
}
