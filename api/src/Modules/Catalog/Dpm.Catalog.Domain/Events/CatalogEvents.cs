using Dpm.BuildingBlocks.Domain;

namespace Dpm.Catalog.Domain.Events;

public sealed record ProductSubmitted(Guid ProductPublicId, Guid StorePublicId) : DomainEvent;

public sealed record ProductApproved(Guid ProductPublicId) : DomainEvent;

public sealed record ProductRejected(Guid ProductPublicId, string Reason) : DomainEvent;

public sealed record ProductPublished(Guid ProductPublicId, string Slug) : DomainEvent;

public sealed record ProductUnpublished(Guid ProductPublicId) : DomainEvent;

public sealed record ProductVersionReleased(Guid ProductPublicId, string VersionNumber) : DomainEvent;
