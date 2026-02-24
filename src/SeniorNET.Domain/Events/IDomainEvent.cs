using MediatR;

namespace SeniorNET.Domain.Events;

public interface IDomainEvent : INotification
{
    DateTime OccurredOn { get; }
}
