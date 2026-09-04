namespace Dpm.BuildingBlocks.Domain;

public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid PublicId { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
