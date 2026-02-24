using MediatR;
using SeniorNET.Application.DTOs;
using SeniorNET.Application.Interfaces;
using SeniorNET.Domain.Interfaces;

namespace SeniorNET.Application.Queries.GetOrder;

public sealed class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, OrderDto?>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICacheService _cache;

    public GetOrderQueryHandler(IOrderRepository orderRepository, ICacheService cache)
    {
        _orderRepository = orderRepository;
        _cache = cache;
    }

    public async Task<OrderDto?> Handle(GetOrderQuery request, CancellationToken ct)
    {
        var cacheKey = $"order:{request.OrderId}";
        var cached = await _cache.GetAsync<OrderDto>(cacheKey, ct);
        if (cached is not null)
            return cached;

        var order = await _orderRepository.GetByIdAsync(request.OrderId, ct);
        if (order is null)
            return null;

        var dto = order.ToDto();
        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), ct);
        return dto;
    }
}
