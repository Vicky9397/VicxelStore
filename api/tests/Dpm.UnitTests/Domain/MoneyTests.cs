using Dpm.BuildingBlocks.Domain;
using FluentAssertions;

namespace Dpm.UnitTests.Domain;

public sealed class MoneyTests
{
    [Fact]
    public void Create_normalizes_the_currency_code()
    {
        var money = Money.Create(100m, "inr");

        money.IsSuccess.Should().BeTrue();
        money.Value.Currency.Should().Be("INR");
    }

    [Theory]
    [InlineData("IN")]
    [InlineData("RUPEE")]
    [InlineData("")]
    public void Create_rejects_a_malformed_currency_code(string currency)
    {
        var money = Money.Create(100m, currency);

        money.IsFailure.Should().BeTrue();
        money.Error.Code.Should().Be("MONEY_CURRENCY");
    }

    [Fact]
    public void Create_rejects_an_unsupported_currency()
    {
        var money = Money.Create(100m, "JPY");

        money.IsFailure.Should().BeTrue();
        money.Error.Code.Should().Be("MONEY_CURRENCY");
    }

    [Fact]
    public void Create_rejects_more_than_four_decimal_places()
    {
        var money = Money.Create(1.000001m, "INR");

        money.IsFailure.Should().BeTrue();
        money.Error.Code.Should().Be("MONEY_PRECISION");
    }

    [Fact]
    public void Arithmetic_across_currencies_throws()
    {
        var rupees = Money.Create(100m, "INR").Value;
        var dollars = Money.Create(100m, "USD").Value;

        var add = () => rupees.Add(dollars);

        add.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Commission_split_of_a_sale_matches_the_specification_example()
    {
        // Spec 03 section 3.3: 1,000 INR sale at 15% commission leaves 850 for the seller.
        var gross = Money.Create(1000m, "INR").Value;

        var commission = gross.MultiplyPercent(15m);
        var sellerEarning = gross.Subtract(commission);

        commission.Amount.Should().Be(150m);
        sellerEarning.Amount.Should().Be(850m);
    }

    [Fact]
    public void Money_is_compared_by_value()
    {
        Money.Create(10m, "INR").Value.Should().Be(Money.Create(10m, "INR").Value);
        Money.Create(10m, "INR").Value.Should().NotBe(Money.Create(10m, "USD").Value);
    }
}
