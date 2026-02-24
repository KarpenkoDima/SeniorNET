namespace SeniorNET.Domain.Events;

public sealed record OrderItemAddedEvent(
    Guid OrderId,
    Guid OrderItemId,
    string ProductName,
    int Quantity,
    DateTime OccurredOn) : IDomainEvent;
