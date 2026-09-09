using Dpm.BuildingBlocks.Domain;
using Dpm.Payments.Application.Abstractions;
using Dpm.Payments.Contracts;
using Dpm.Payments.Domain;

namespace Dpm.Payments.Application.Intents;

/// <summary>
/// Creates the provider intent for an order and records the payment in Created.
/// Nothing is captured here: capture only ever follows a verified webhook, so a
/// client cannot assert that it paid.
/// </summary>
public sealed class CreatePaymentIntentService(
    IPaymentRepository payments,
    IPaymentsUnitOfWork unitOfWork,
    IPaymentProviderFactory providerFactory,
    IClock clock)
    : IPaymentIntentService
{
    public async Task<PaymentIntentCreated> CreateForOrderAsync(
        CreatePaymentIntent request,
        CancellationToken ct)
    {
        if (!providerFactory.IsSupported(request.ProviderName))
        {
            throw new ArgumentException($"Unsupported payment provider '{request.ProviderName}'.", nameof(request));
        }

        var amount = Money.Create(request.Amount, request.Currency);
        if (amount.IsFailure)
        {
            throw new ArgumentException(amount.Error.Message, nameof(request));
        }

        var provider = providerFactory.Resolve(request.ProviderName);
        var intent = await provider.CreateIntentAsync(
            new CreateIntent(request.OrderPublicId, amount.Value, request.ReturnUrl), ct);

        var payment = Payment.Create(
            request.OrderId, provider.Name, intent.ProviderIntentId, amount.Value, clock);
        payments.Add(payment);
        await unitOfWork.SaveChangesAsync(ct);

        return new PaymentIntentCreated(
            payment.PublicId, provider.Name, intent.ClientSecret, intent.Instructions);
    }
}
