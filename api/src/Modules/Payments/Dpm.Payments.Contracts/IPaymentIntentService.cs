namespace Dpm.Payments.Contracts;

/// <summary>
/// The Payments module's public surface. Orders drives payment creation through
/// this and never touches a provider SDK or pay.* directly.
/// </summary>
public interface IPaymentIntentService
{
    Task<PaymentIntentCreated> CreateForOrderAsync(CreatePaymentIntent request, CancellationToken ct);
}

public sealed record CreatePaymentIntent(
    long OrderId,
    Guid OrderPublicId,
    decimal Amount,
    string Currency,
    string ProviderName,
    string? ReturnUrl);

/// <param name="ClientSecret">
/// Handed to the buyer's browser to complete the charge. It authorizes exactly
/// one payment and is not a credential for anything else.
/// </param>
public sealed record PaymentIntentCreated(
    Guid PaymentPublicId,
    string Provider,
    string ClientSecret,
    string? Instructions);
