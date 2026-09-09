using Dpm.BuildingBlocks.Domain;

namespace Dpm.Payments.Domain.Events;

public sealed record PaymentFailedRecorded(Guid PaymentPublicId, long OrderId, string Reason) : DomainEvent;
