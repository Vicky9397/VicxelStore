using Dpm.BuildingBlocks.Domain;

namespace Dpm.Orders.Domain.Events;

public sealed record OrderPlaced(Guid OrderPublicId, Guid BuyerPublicId, decimal GrandTotal, string Currency)
    : DomainEvent;

public sealed record LicenseIssued(Guid LicensePublicId, Guid OrderPublicId, long VariantId) : DomainEvent;

public sealed record InvoiceGenerated(Guid OrderPublicId, string InvoiceNo) : DomainEvent;

public sealed record OrderCancelled(Guid OrderPublicId, string Reason) : DomainEvent;
