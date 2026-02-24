using MediatR;
using SeniorNET.Application.DTOs;

namespace SeniorNET.Application.Commands.CreateOrder;

public sealed record CreateOrderCommand(
    string CustomerId,
    string Street,
    string City,
    string ZipCode,
    string Country,
    List<CreateOrderItemCommand> Items) : IRequest<OrderDto>;

public sealed record CreateOrderItemCommand(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    string Currency,
    int Quantity);
