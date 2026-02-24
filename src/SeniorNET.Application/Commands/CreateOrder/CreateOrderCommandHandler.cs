using MediatR;
using SeniorNET.Application.DTOs;
using SeniorNET.Domain.Entities;
using SeniorNET.Domain.Interfaces;
using SeniorNET.Domain.ValueObjects;

namespace SeniorNET.Application.Commands.CreateOrder;

public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderDto>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrderCommandHandler(IOrderRepository orderRepository, IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        var address = Address.Create(request.Street, request.City, request.ZipCode, request.Country);
        var order = Order.Create(request.CustomerId, address);

        foreach (var item in request.Items)
        {
            var unitPrice = Money.Create(item.UnitPrice, item.Currency);
            order.AddItem(item.ProductId, item.ProductName, unitPrice, item.Quantity);
        }

        order.Confirm();

        await _orderRepository.AddAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return order.ToDto();
    }
}
