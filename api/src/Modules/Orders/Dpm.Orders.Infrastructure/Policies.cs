using System.Text.Json;
using Dpm.BuildingBlocks.Domain;
using Dpm.Catalog.Contracts;
using Dpm.Marketplace.Contracts;
using Dpm.Orders.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Dpm.Orders.Infrastructure;

/// <summary>
/// Reads platform settings from admin.Settings. Commission, tax and hold values
/// are financial policy, so they are configuration rather than constants in code
/// (spec 11B section 13.5).
/// </summary>
public sealed class PlatformSettings(IConfiguration configuration)
{
    public async Task<string?> ReadAsync(string key, CancellationToken ct)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("Database"));
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ValueJson FROM admin.Settings WHERE [Key] = @Key;";
        command.Parameters.Add(new SqlParameter("@Key", key));

        await connection.OpenAsync(ct);
        var value = await command.ExecuteScalarAsync(ct);
        return value as string;
    }
}

/// <summary>
/// GST and VAT by buyer region (spec 03 section 3.2). Rates come from the
/// tax.rules setting; an unknown region attracts no tax rather than a guess.
/// </summary>
public sealed class SettingsTaxCalculator(PlatformSettings settings) : ITaxCalculator
{
    public async Task<Money> CalculateAsync(Money taxableAmount, string billingCountry, CancellationToken ct)
    {
        var json = await settings.ReadAsync("tax.rules", ct);
        if (json is null)
        {
            return Money.Zero(taxableAmount.Currency);
        }

        using var document = JsonDocument.Parse(json);
        var country = billingCountry.ToUpperInvariant();

        // EU member states share one VAT entry in the seeded rules.
        if (!document.RootElement.TryGetProperty(country, out var rule)
            && !document.RootElement.TryGetProperty("EU", out rule))
        {
            return Money.Zero(taxableAmount.Currency);
        }

        if (!rule.TryGetProperty("ratePct", out var rateElement))
        {
            return Money.Zero(taxableAmount.Currency);
        }

        return taxableAmount.MultiplyPercent(rateElement.GetDecimal());
    }
}

/// <summary>
/// Effective commission = seller override, else category rate, else platform
/// default (spec 03 section 3.4). Resolved once at checkout and frozen onto the
/// order line.
/// </summary>
public sealed class CommissionPolicy(
    PlatformSettings settings,
    IStoreDirectory stores,
    ICatalogDirectory catalog)
    : ICommissionPolicy
{
    private const decimal FallbackRate = 15.00m;

    public async Task<decimal> ResolveRateAsync(long storeId, int categoryId, CancellationToken ct)
    {
        var sellerOverride = await stores.FindCommissionOverrideAsync(storeId, ct);
        if (sellerOverride is { } overrideRate)
        {
            return overrideRate;
        }

        var categoryRate = await catalog.FindCategoryCommissionAsync(categoryId, ct);
        if (categoryRate is { } rate)
        {
            return rate;
        }

        var json = await settings.ReadAsync("commission.defaultPct", ct);
        if (json is null)
        {
            return FallbackRate;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("value", out var value)
            ? value.GetDecimal()
            : FallbackRate;
    }
}

public sealed class OrderPolicy(PlatformSettings settings) : IOrderPolicy
{
    private static readonly TimeSpan FallbackHold = TimeSpan.FromDays(7);

    public async Task<TimeSpan> GetPayoutHoldPeriodAsync(CancellationToken ct)
    {
        var json = await settings.ReadAsync("payout.holdDays", ct);
        if (json is null)
        {
            return FallbackHold;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("value", out var value)
            ? TimeSpan.FromDays(value.GetInt32())
            : FallbackHold;
    }
}
