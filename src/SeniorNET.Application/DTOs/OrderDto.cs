namespace SeniorNET.Application.DTOs;

public sealed record OrderDto(
    Guid Id,
    string CustomerId,
    string Status,
    string ShippingAddress,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
