namespace SeniorNET.Domain.Events;

public sealed record OrderCreatedEvent(Guid OrderId, string CustomerId, DateTime OccurredOn) : IDomainEvent;
