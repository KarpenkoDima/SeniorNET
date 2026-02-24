using FluentAssertions;
using SeniorNET.Domain.Entities;
using SeniorNET.Domain.Enums;
using SeniorNET.Domain.Events;
using SeniorNET.Domain.Exceptions;
using SeniorNET.Domain.ValueObjects;

namespace SeniorNET.Domain.Tests;

public class OrderTests
{
    private static Order CreateTestOrder()
    {
        var address = Address.Create("123 Main St", "Kyiv", "01001", "Ukraine");
        return Order.Create("customer-1", address);
    }

    [Fact]
    public void Create_ShouldSetPendingStatus_AndRaiseDomainEvent()
    {
        var order = CreateTestOrder();

        order.Status.Should().Be(OrderStatus.Pending);
        order.CustomerId.Should().Be("customer-1");
        order.Items.Should().BeEmpty();
        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderCreatedEvent>();
    }

    [Fact]
    public void Create_WithEmptyCustomerId_ShouldThrow()
    {
        var address = Address.Create("Street", "City", "12345", "UA");
        var act = () => Order.Create("", address);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddItem_ToPendingOrder_ShouldSucceed()
    {
        var order = CreateTestOrder();
        var price = Money.Create(29.99m, "USD");

        order.AddItem(Guid.NewGuid(), "Widget", price, 2);

        order.Items.Should().HaveCount(1);
        order.Items[0].ProductName.Should().Be("Widget");
        order.Items[0].Quantity.Should().Be(2);
        order.TotalAmount.Amount.Should().Be(59.98m);
    }

    [Fact]
    public void AddItem_SameProduct_ShouldIncreaseQuantity()
    {
        var order = CreateTestOrder();
        var productId = Guid.NewGuid();
        var price = Money.Create(10, "USD");

        order.AddItem(productId, "Widget", price, 2);
        order.AddItem(productId, "Widget", price, 3);

        order.Items.Should().HaveCount(1);
        order.Items[0].Quantity.Should().Be(5);
        order.TotalAmount.Amount.Should().Be(50);
    }

    [Fact]
    public void AddItem_ToConfirmedOrder_ShouldThrow()
    {
        var order = CreateTestOrder();
        order.AddItem(Guid.NewGuid(), "Widget", Money.Create(10, "USD"), 1);
        order.Confirm();

        var act = () => order.AddItem(Guid.NewGuid(), "Another", Money.Create(5, "USD"), 1);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RemoveItem_ExistingItem_ShouldSucceed()
    {
        var order = CreateTestOrder();
        var price = Money.Create(10, "USD");
        var item = order.AddItem(Guid.NewGuid(), "Widget", price, 1);

        order.RemoveItem(item.Id);

        order.Items.Should().BeEmpty();
    }

    [Fact]
    public void RemoveItem_NonExistentItem_ShouldThrow()
    {
        var order = CreateTestOrder();
        var act = () => order.RemoveItem(Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Confirm_WithItems_ShouldChangeStatus()
    {
        var order = CreateTestOrder();
        order.AddItem(Guid.NewGuid(), "Widget", Money.Create(10, "USD"), 1);

        order.Confirm();

        order.Status.Should().Be(OrderStatus.Confirmed);
        order.DomainEvents.Should().Contain(e => e is OrderStatusChangedEvent);
    }

    [Fact]
    public void Confirm_WithNoItems_ShouldThrow()
    {
        var order = CreateTestOrder();
        var act = () => order.Confirm();
        act.Should().Throw<DomainException>().WithMessage("*no items*");
    }

    [Fact]
    public void FullLifecycle_ShouldTransitionCorrectly()
    {
        var order = CreateTestOrder();
        order.AddItem(Guid.NewGuid(), "Widget", Money.Create(10, "USD"), 1);

        order.Confirm();
        order.Status.Should().Be(OrderStatus.Confirmed);

        order.StartProcessing();
        order.Status.Should().Be(OrderStatus.Processing);

        order.Ship();
        order.Status.Should().Be(OrderStatus.Shipped);

        order.Deliver();
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void Cancel_PendingOrder_ShouldSucceed()
    {
        var order = CreateTestOrder();
        order.Cancel();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShippedOrder_ShouldThrow()
    {
        var order = CreateTestOrder();
        order.AddItem(Guid.NewGuid(), "Widget", Money.Create(10, "USD"), 1);
        order.Confirm();
        order.StartProcessing();
        order.Ship();

        var act = () => order.Cancel();
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Ship_WithoutProcessing_ShouldThrow()
    {
        var order = CreateTestOrder();
        order.AddItem(Guid.NewGuid(), "Widget", Money.Create(10, "USD"), 1);
        order.Confirm();

        var act = () => order.Ship();
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void TotalAmount_MultipleItems_ShouldSumCorrectly()
    {
        var order = CreateTestOrder();
        order.AddItem(Guid.NewGuid(), "Widget A", Money.Create(10, "USD"), 3);
        order.AddItem(Guid.NewGuid(), "Widget B", Money.Create(25.50m, "USD"), 2);

        order.TotalAmount.Amount.Should().Be(81.00m);
        order.TotalAmount.Currency.Should().Be("USD");
    }
}
