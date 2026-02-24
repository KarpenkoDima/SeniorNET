using SeniorNET.Domain.Enums;

namespace SeniorNET.Domain.Events;

public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    OrderStatus OldStatus,
    OrderStatus NewStatus,
    DateTime OccurredOn) : IDomainEvent;
