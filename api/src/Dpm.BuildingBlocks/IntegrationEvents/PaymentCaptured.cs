using Dpm.BuildingBlocks.Domain;

namespace Dpm.BuildingBlocks.IntegrationEvents;

/// <summary>
/// Published by Payments when a provider confirms a capture. Orders consumes it
/// to issue licenses and Ledger consumes it to post the sale journal.
///
/// It lives in the shared kernel because both consumers sit on different sides
/// of the module graph and neither may reference the other's assembly.
/// Delivery is at-least-once through the outbox, so both handlers are written to
/// be replay-safe: a second delivery must not issue a second license or a second
/// journal entry (spec 03 section 3.6).
/// </summary>
/// <param name="GatewayFeeAmount">
/// The provider's processing fee, recovered from the gross before the seller is
/// credited (spec 01 section 1.3).
/// </param>
public sealed record PaymentCaptured(
    Guid PaymentPublicId,
    long OrderId,
    decimal Amount,
    string Currency,
    decimal GatewayFeeAmount) : DomainEvent;
