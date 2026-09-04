using Dpm.BuildingBlocks.Domain;

namespace Dpm.Marketplace.Domain.Events;

public sealed record StoreCreated(Guid StorePublicId, Guid OwnerUserPublicId, string Slug) : DomainEvent;

public sealed record SellerVerified(Guid StorePublicId) : DomainEvent;

public sealed record StoreSuspended(Guid StorePublicId, string Reason) : DomainEvent;

public sealed record StoreReinstated(Guid StorePublicId) : DomainEvent;
