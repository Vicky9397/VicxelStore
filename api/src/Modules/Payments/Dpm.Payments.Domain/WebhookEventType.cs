namespace Dpm.Payments.Domain;

/// <summary>
/// Canonical event vocabulary every provider maps its native events onto
/// (spec 03 section 3.1), so the application never branches on a provider name.
/// </summary>
public enum WebhookEventType
{
    Unknown = 0,
    PaymentSucceeded,
    PaymentFailed,
    ChargeRefunded,
    ChargebackOpened,
    ChargebackWon,
    ChargebackLost,
    PayoutPaid,
    PayoutFailed,
}
