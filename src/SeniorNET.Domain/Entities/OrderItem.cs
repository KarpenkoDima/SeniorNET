using SeniorNET.Domain.ValueObjects;

namespace SeniorNET.Domain.Entities;

public sealed class OrderItem : Entity
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public Money UnitPrice { get; private set; } = null!;
    public int Quantity { get; private set; }

    // EF Core requires parameterless constructor
    private OrderItem() { }

    internal OrderItem(Guid productId, string productName, Money unitPrice, int quantity)
    {
        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("Product name is required.", nameof(productName));
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));

        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public Money TotalPrice => UnitPrice.Multiply(Quantity);

    public void UpdateQuantity(int newQuantity)
    {
        if (newQuantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(newQuantity));
        Quantity = newQuantity;
    }
}
