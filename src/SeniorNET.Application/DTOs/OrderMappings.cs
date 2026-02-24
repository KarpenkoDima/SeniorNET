using SeniorNET.Domain.Entities;

namespace SeniorNET.Application.DTOs;

public static class OrderMappings
{
    public static OrderDto ToDto(this Order order) => new(
        order.Id,
        order.CustomerId,
        order.Status.ToString(),
        order.ShippingAddress.ToString(),
        order.TotalAmount.Amount,
        order.TotalAmount.Currency,
        order.CreatedAt,
        order.UpdatedAt,
        order.Items.Select(i => i.ToDto()).ToList());

    public static OrderItemDto ToDto(this OrderItem item) => new(
        item.Id,
        item.ProductId,
        item.ProductName,
        item.UnitPrice.Amount,
        item.Quantity,
        item.TotalPrice.Amount);
}
