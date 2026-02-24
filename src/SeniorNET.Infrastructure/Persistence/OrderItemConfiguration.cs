using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorNET.Domain.Entities;

namespace SeniorNET.Infrastructure.Persistence;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductId).IsRequired();

        builder.Property(i => i.ProductName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.Quantity).IsRequired();

        builder.OwnsOne(i => i.UnitPrice, m =>
        {
            m.Property(p => p.Amount).HasColumnName("unit_price").HasPrecision(18, 2).IsRequired();
            m.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        builder.Ignore(i => i.TotalPrice);
        builder.Ignore(i => i.DomainEvents);

        builder.HasIndex(i => i.ProductId).HasDatabaseName("ix_order_items_product_id");
    }
}
