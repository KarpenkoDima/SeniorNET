using FluentAssertions;
using NSubstitute;
using SeniorNET.Application.Commands.CancelOrder;
using SeniorNET.Application.Interfaces;
using SeniorNET.Domain.Entities;
using SeniorNET.Domain.Enums;
using SeniorNET.Domain.Exceptions;
using SeniorNET.Domain.Interfaces;
using SeniorNET.Domain.ValueObjects;

namespace SeniorNET.Application.Tests;

public class CancelOrderCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();

    private CancelOrderCommandHandler CreateHandler() => new(_orderRepository, _unitOfWork, _cache);

    private static Order CreateConfirmedOrder()
    {
        var address = Address.Create("Street", "City", "12345", "Country");
        var order = Order.Create("customer-1", address);
        order.AddItem(Guid.NewGuid(), "Widget", Money.Create(10, "USD"), 1);
        order.Confirm();
        return order;
    }

    [Fact]
    public async Task Handle_ExistingPendingOrder_ShouldCancel()
    {
        var address = Address.Create("Street", "City", "12345", "Country");
        var order = Order.Create("customer-1", address);
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var handler = CreateHandler();
        await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Cancelled);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync($"order:{order.Id}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonExistentOrder_ShouldThrow()
    {
        _orderRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Order?)null);

        var handler = CreateHandler();
        var act = () => handler.Handle(new CancelOrderCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShippedOrder_ShouldThrowDomainException()
    {
        var order = CreateConfirmedOrder();
        order.StartProcessing();
        order.Ship();
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var handler = CreateHandler();
        var act = () => handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }
}
