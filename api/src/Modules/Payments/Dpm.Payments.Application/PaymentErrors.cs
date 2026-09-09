using Dpm.BuildingBlocks.Application;

namespace Dpm.Payments.Application;

public static class PaymentErrors
{
    public static readonly Error UnsupportedProvider =
        Error.Validation("VALIDATION_ERROR", "That payment method is not available.");

    public static readonly Error PaymentNotFound =
        Error.NotFound("NOT_FOUND", "Payment not found.");

    public static readonly Error IntentFailed =
        Error.PaymentFailed("PAYMENT_FAILED", "The payment could not be started. Try another method.");

    /// <summary>
    /// A webhook whose signature does not verify. It is never processed and
    /// never distinguished from noise in the response, so a caller cannot probe
    /// for a valid signature.
    /// </summary>
    public static readonly Error WebhookUnverified =
        Error.Unauthorized("UNAUTHENTICATED", "Webhook signature verification failed.");
}
