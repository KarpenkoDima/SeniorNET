using FluentAssertions;
using NSubstitute;
using SeniorNET.Application.Commands.CreateOrder;
using SeniorNET.Application.Interfaces;
using SeniorNET.Domain.Entities;
using SeniorNET.Domain.Enums;
using SeniorNET.Domain.Interfaces;

namespace SeniorNET.Application.Tests;

public class CreateOrderCommandHandlerTests
{
    private readonly IOrderRepository _orderRepository = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();

    private CreateOrderCommandHandler CreateHandler() => new(_orderRepository, _unitOfWork, _cache);

    private static CreateOrderCommand CreateValidCommand() => new(
        CustomerId: "customer-123",
        Street: "123 Main St",
        City: "Kyiv",
        ZipCode: "01001",
        Country: "Ukraine",
        Items:
        [
            new CreateOrderItemCommand(Guid.NewGuid(), "Widget", 29.99m, "USD", 2),
            new CreateOrderItemCommand(Guid.NewGuid(), "Gadget", 49.99m, "USD", 1)
        ]);

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateOrderAndReturnDto()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.CustomerId.Should().Be("customer-123");
        result.Status.Should().Be(OrderStatus.Confirmed.ToString());
        result.Items.Should().HaveCount(2);
        result.TotalAmount.Should().Be(109.97m);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldPersistOrder()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        await handler.Handle(command, CancellationToken.None);

        await _orderRepository.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCacheResult()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        await _cache.Received(1).SetAsync(
            $"order:{result.Id}",
            Arg.Any<DTOs.OrderDto>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }
}
