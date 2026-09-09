using Dpm.Payments.Application.Abstractions;

namespace Dpm.Payments.Infrastructure.Providers;

public sealed class PaymentProviderFactory(IEnumerable<IPaymentProvider> providers) : IPaymentProviderFactory
{
    private readonly Dictionary<string, IPaymentProvider> _providers =
        providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> SupportedProviders => _providers.Keys.ToList();

    public bool IsSupported(string providerName) =>
        !string.IsNullOrWhiteSpace(providerName) && _providers.ContainsKey(providerName);

    public IPaymentProvider Resolve(string providerName) =>
        _providers.TryGetValue(providerName, out var provider)
            ? provider
            : throw new KeyNotFoundException($"No payment provider named '{providerName}' is registered.");
}
