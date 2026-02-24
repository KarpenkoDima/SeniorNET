using SeniorNET.Domain.Enums;
using SeniorNET.Domain.Events;
using SeniorNET.Domain.Exceptions;
using SeniorNET.Domain.ValueObjects;

namespace SeniorNET.Domain.Entities;

public sealed class Order : Entity
{
    private readonly List<OrderItem> _items = [];

    public string CustomerId { get; private set; } = string.Empty;
    public Address ShippingAddress { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    // EF Core
    private Order() { }

    public static Order Create(string customerId, Address shippingAddress)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("Customer ID is required.", nameof(customerId));

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            ShippingAddress = shippingAddress,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        order.RaiseDomainEvent(new OrderCreatedEvent(order.Id, customerId, DateTime.UtcNow));
        return order;
    }

    public OrderItem AddItem(Guid productId, string productName, Money unitPrice, int quantity)
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException($"Cannot add items to an order with status '{Status}'.");

        var existingItem = _items.Find(i => i.ProductId == productId);
        if (existingItem is not null)
        {
            existingItem.UpdateQuantity(existingItem.Quantity + quantity);
            UpdatedAt = DateTime.UtcNow;
            return existingItem;
        }

        var item = new OrderItem(productId, productName, unitPrice, quantity);
        _items.Add(item);
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new OrderItemAddedEvent(Id, item.Id, productName, quantity, DateTime.UtcNow));
        return item;
    }

    public void RemoveItem(Guid orderItemId)
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException($"Cannot remove items from an order with status '{Status}'.");

        var item = _items.Find(i => i.Id == orderItemId)
            ?? throw new DomainException($"Order item '{orderItemId}' not found.");

        _items.Remove(item);
        UpdatedAt = DateTime.UtcNow;
    }

    public Money TotalAmount
    {
        get
        {
            if (_items.Count == 0)
                return Money.Zero();

            var total = Money.Zero(_items[0].UnitPrice.Currency);
            foreach (var item in _items)
                total = total.Add(item.TotalPrice);
            return total;
        }
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException($"Cannot confirm order with status '{Status}'.");
        if (_items.Count == 0)
            throw new DomainException("Cannot confirm an order with no items.");

        ChangeStatus(OrderStatus.Confirmed);
    }

    public void StartProcessing()
    {
        if (Status != OrderStatus.Confirmed)
            throw new DomainException($"Cannot process order with status '{Status}'.");
        ChangeStatus(OrderStatus.Processing);
    }

    public void Ship()
    {
        if (Status != OrderStatus.Processing)
            throw new DomainException($"Cannot ship order with status '{Status}'.");
        ChangeStatus(OrderStatus.Shipped);
    }

    public void Deliver()
    {
        if (Status != OrderStatus.Shipped)
            throw new DomainException($"Cannot deliver order with status '{Status}'.");
        ChangeStatus(OrderStatus.Delivered);
    }

    public void Cancel()
    {
        if (Status is OrderStatus.Shipped or OrderStatus.Delivered)
            throw new DomainException($"Cannot cancel order with status '{Status}'.");
        ChangeStatus(OrderStatus.Cancelled);
    }

    private void ChangeStatus(OrderStatus newStatus)
    {
        var oldStatus = Status;
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, newStatus, DateTime.UtcNow));
    }
}
