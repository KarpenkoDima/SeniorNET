using MediatR;
using SeniorNET.Application.Interfaces;
using SeniorNET.Domain.Interfaces;

namespace SeniorNET.Application.Commands.CancelOrder;

public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public CancelOrderCommandHandler(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ICacheService cache)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task Handle(CancelOrderCommand request, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, ct)
            ?? throw new KeyNotFoundException($"Order '{request.OrderId}' not found.");

        order.Cancel();

        await _orderRepository.UpdateAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync($"order:{request.OrderId}", ct);
    }
}
