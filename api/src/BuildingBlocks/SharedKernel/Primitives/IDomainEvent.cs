using MediatR;

namespace JobTracker.SharedKernel.Primitives;

public interface IDomainEvent : INotification
{
    Guid Id { get; }
    DateTimeOffset OccurredOn { get; }
}
