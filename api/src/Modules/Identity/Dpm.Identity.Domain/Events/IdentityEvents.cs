using Dpm.BuildingBlocks.Domain;

namespace Dpm.Identity.Domain.Events;

public sealed record UserRegistered(Guid UserPublicId, string Email) : DomainEvent;

public sealed record EmailVerificationRequested(Guid UserPublicId, string Email) : DomainEvent;

public sealed record EmailVerified(Guid UserPublicId) : DomainEvent;

public sealed record UserLockedOut(Guid UserPublicId, DateTime LockoutEndUtc) : DomainEvent;

public sealed record RefreshTokenReuseDetected(Guid UserPublicId, Guid FamilyId) : DomainEvent;
