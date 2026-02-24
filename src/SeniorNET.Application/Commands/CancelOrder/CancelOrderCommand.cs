using MediatR;

namespace SeniorNET.Application.Commands.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId) : IRequest;
