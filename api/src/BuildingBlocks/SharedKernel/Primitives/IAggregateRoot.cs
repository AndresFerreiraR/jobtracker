namespace JobTracker.SharedKernel.Primitives;

public interface IAggregateRoot
{
    IReadOnlyCollection<IDomainEvent> Events { get; }
    IReadOnlyCollection<IDomainEvent> DrainEvents();
}
