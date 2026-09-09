namespace Dpm.Payments.Infrastructure.Providers;

public sealed class PaymentProviderOptions
{
    public const string SectionName = "Payments";

    /// <summary>Provider used when checkout does not name one.</summary>
    public string DefaultProvider { get; init; } = "sandbox";

    /// <summary>
    /// HMAC secret the sandbox provider signs its webhooks with. It exists so the
    /// signature-verification path is exercised end to end in development; a real
    /// provider's secret comes from the environment's secret store.
    /// </summary>
    public string SandboxWebhookSecret { get; init; } = string.Empty;

    /// <summary>Sandbox processing fee in percent, mirroring a card gateway's pricing.</summary>
    public decimal SandboxFeePct { get; init; } = 2.5m;

    public ManualBankOptions ManualBank { get; init; } = new();
}

public sealed class ManualBankOptions
{
    public string AccountName { get; init; } = "VicxelStore Holdings";

    public string AccountNumberMasked { get; init; } = "XXXX-XXXX-4321";

    public string Ifsc { get; init; } = "DEMO0000123";
}
