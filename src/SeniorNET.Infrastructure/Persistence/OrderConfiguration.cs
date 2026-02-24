using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorNET.Domain.Entities;

namespace SeniorNET.Infrastructure.Persistence;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.CustomerId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.UpdatedAt);

        builder.OwnsOne(o => o.ShippingAddress, a =>
        {
            a.Property(p => p.Street).HasColumnName("shipping_street").HasMaxLength(200).IsRequired();
            a.Property(p => p.City).HasColumnName("shipping_city").HasMaxLength(100).IsRequired();
            a.Property(p => p.ZipCode).HasColumnName("shipping_zip").HasMaxLength(20).IsRequired();
            a.Property(p => p.Country).HasColumnName("shipping_country").HasMaxLength(100).IsRequired();
        });

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey("OrderId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(o => o.DomainEvents);
        builder.Ignore(o => o.TotalAmount);

        builder.HasIndex(o => o.CustomerId).HasDatabaseName("ix_orders_customer_id");
        builder.HasIndex(o => o.Status).HasDatabaseName("ix_orders_status");
        builder.HasIndex(o => o.CreatedAt).HasDatabaseName("ix_orders_created_at");
    }
}
