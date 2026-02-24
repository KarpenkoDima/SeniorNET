using MediatR;
using SeniorNET.Application.DTOs;

namespace SeniorNET.Application.Queries.GetOrder;

public sealed record GetOrderQuery(Guid OrderId) : IRequest<OrderDto?>;
