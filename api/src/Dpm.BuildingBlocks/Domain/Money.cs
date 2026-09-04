using Dpm.BuildingBlocks.Application;

namespace Dpm.BuildingBlocks.Domain;

/// <summary>
/// Money value object: decimal(19,4) amount + ISO-4217 currency. Arithmetic across
/// currencies is a programming error and throws.
/// </summary>
public sealed class Money : ValueObject
{
    private static readonly HashSet<string> SupportedCurrencies = ["INR", "USD", "EUR", "GBP"];

    public decimal Amount { get; }

    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Result<Money> Create(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            return Result.Failure<Money>(Error.Validation("MONEY_CURRENCY", "Currency must be a 3-letter ISO-4217 code."));
        }

        var normalized = currency.ToUpperInvariant();
        if (!SupportedCurrencies.Contains(normalized))
        {
            return Result.Failure<Money>(Error.Validation("MONEY_CURRENCY", $"Currency '{normalized}' is not supported."));
        }

        if (decimal.Round(amount, 4) != amount)
        {
            return Result.Failure<Money>(Error.Validation("MONEY_PRECISION", "Amount cannot have more than 4 decimal places."));
        }

        return Result.Success(new Money(amount, normalized));
    }

    public static Money Zero(string currency) => Create(0m, currency).Value;

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money MultiplyPercent(decimal percent) =>
        new(decimal.Round(Amount * percent / 100m, 4, MidpointRounding.ToEven), Currency);

    public bool IsNegative => Amount < 0;

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException($"Currency mismatch: {Currency} vs {other.Currency}.");
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount:0.0000} {Currency}";
}
