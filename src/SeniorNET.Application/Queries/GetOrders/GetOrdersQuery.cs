using MediatR;
using SeniorNET.Application.DTOs;

namespace SeniorNET.Application.Queries.GetOrders;

public sealed record GetOrdersQuery(int Page = 1, int PageSize = 20) : IRequest<PagedResult<OrderDto>>;
