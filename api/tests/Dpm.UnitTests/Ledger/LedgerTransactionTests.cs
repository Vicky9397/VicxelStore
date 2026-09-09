using Dpm.BuildingBlocks.Domain;
using Dpm.Ledger.Domain;
using FluentAssertions;

namespace Dpm.UnitTests.Ledger;

public sealed class LedgerTransactionTests
{
    private readonly TestClock _clock = new();

    private static Money Inr(decimal amount) => Money.Create(amount, "INR").Value;

    [Fact]
    public void An_unbalanced_journal_cannot_be_posted()
    {
        var lines = new[]
        {
            LedgerLine.Debited(LedgerAccount.CashGateway, Inr(100m)).Value,
            LedgerLine.Credited(LedgerAccount.PlatformRevenue, Inr(90m)).Value,
        };

        var posted = LedgerTransaction.Post(LedgerRefType.Order, 1, "Bad", lines, _clock);

        posted.IsFailure.Should().BeTrue();
        posted.Error.Code.Should().Be("LEDGER_UNBALANCED");
    }

    [Fact]
    public void A_journal_must_balance_within_each_currency_not_merely_in_total()
    {
        // Debits and credits sum to 100 each overall, but neither currency balances.
        var lines = new[]
        {
            LedgerLine.Debited(LedgerAccount.CashGateway, Inr(100m)).Value,
            LedgerLine.Credited(LedgerAccount.PlatformRevenue, Money.Create(100m, "USD").Value).Value,
        };

        var posted = LedgerTransaction.Post(LedgerRefType.Order, 1, "Cross-currency", lines, _clock);

        posted.IsFailure.Should().BeTrue();
        posted.Error.Code.Should().Be("LEDGER_UNBALANCED");
    }

    [Fact]
    public void An_empty_journal_cannot_be_posted()
    {
        LedgerTransaction.Post(LedgerRefType.Order, 1, "Empty", [], _clock)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_line_against_an_account_outside_the_chart_is_refused()
    {
        var line = LedgerLine.Debited("NotAnAccount", Inr(100m));

        line.IsFailure.Should().BeTrue();
        line.Error.Code.Should().Be("LEDGER_UNKNOWN_ACCOUNT");
    }

    [Fact]
    public void A_negative_amount_is_refused_because_the_other_side_expresses_it()
    {
        LedgerLine.Debited(LedgerAccount.CashGateway, Inr(-1m))
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_line_is_never_both_a_debit_and_a_credit()
    {
        var debit = LedgerLine.Debited(LedgerAccount.CashGateway, Inr(100m)).Value;
        var credit = LedgerLine.Credited(LedgerAccount.TaxPayable, Inr(100m)).Value;

        debit.Credit.Should().Be(0m);
        credit.Debit.Should().Be(0m);
    }

    [Fact]
    public void A_balanced_journal_records_its_reference_for_replay_detection()
    {
        var lines = new[]
        {
            LedgerLine.Debited(LedgerAccount.CashGateway, Inr(100m)).Value,
            LedgerLine.Credited(LedgerAccount.PlatformRevenue, Inr(100m)).Value,
        };

        var posted = LedgerTransaction.Post(LedgerRefType.Order, 42, "Sale", lines, _clock);

        posted.IsSuccess.Should().BeTrue();
        posted.Value.RefType.Should().Be(LedgerRefType.Order);
        posted.Value.RefId.Should().Be(42);
        posted.Value.OccurredAtUtc.Should().Be(_clock.UtcNow);
        posted.Value.PublicId.Should().NotBe(Guid.Empty);
    }
}
