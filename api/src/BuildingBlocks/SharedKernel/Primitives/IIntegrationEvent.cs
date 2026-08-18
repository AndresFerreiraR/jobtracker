using MediatR;

namespace JobTracker.SharedKernel.Primitives;

public interface IIntegrationEvent : INotification
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}
