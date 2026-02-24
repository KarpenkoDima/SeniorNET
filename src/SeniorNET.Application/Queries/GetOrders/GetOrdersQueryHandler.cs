using MediatR;
using SeniorNET.Application.DTOs;
using SeniorNET.Domain.Interfaces;

namespace SeniorNET.Application.Queries.GetOrders;

public sealed class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, PagedResult<OrderDto>>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrdersQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<PagedResult<OrderDto>> Handle(GetOrdersQuery request, CancellationToken ct)
    {
        var orders = await _orderRepository.GetAllAsync(request.Page, request.PageSize, ct);
        var totalCount = await _orderRepository.CountAsync(ct);

        return new PagedResult<OrderDto>(
            orders.Select(o => o.ToDto()).ToList(),
            totalCount,
            request.Page,
            request.PageSize);
    }
}
