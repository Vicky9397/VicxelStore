using MediatR;

namespace Dpm.BuildingBlocks.Domain;

public interface IDomainEvent : INotification
{
    Guid EventId { get; }

    DateTime OccurredAtUtc { get; }
}
