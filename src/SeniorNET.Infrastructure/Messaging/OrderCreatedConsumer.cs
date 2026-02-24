using MassTransit;
using Microsoft.Extensions.Logging;
using SeniorNET.Domain.Events;

namespace SeniorNET.Infrastructure.Messaging;

public sealed class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(ILogger<OrderCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        _logger.LogInformation(
            "Order {OrderId} created for customer {CustomerId}",
            context.Message.OrderId,
            context.Message.CustomerId);

        // Here you would trigger downstream processes:
        // - Send confirmation email
        // - Reserve inventory
        // - Notify payment service
        return Task.CompletedTask;
    }
}
