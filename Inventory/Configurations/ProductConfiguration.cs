using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_CurrentStock_NonNegative", "[CurrentStock] >= 0");
            table.HasCheckConstraint("CK_Products_ReservedStock_NonNegative", "[ReservedStock] >= 0");
            table.HasCheckConstraint("CK_Products_ReservedStock_NotOverCurrent", "[ReservedStock] <= [CurrentStock]");
            table.HasCheckConstraint("CK_Products_LowStockThreshold_NonNegative", "[LowStockThreshold] >= 0");
        });
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Sku).HasMaxLength(64).IsRequired();
        builder.Property(product => product.NormalizedSku).HasMaxLength(64).IsRequired();
        builder.Property(product => product.Name).HasMaxLength(200).IsRequired();
        builder.Property(product => product.Unit).HasMaxLength(32).IsRequired();
        builder.Property(product => product.CurrentStock).HasDefaultValue(0).IsRequired();
        builder.Property(product => product.ReservedStock).HasDefaultValue(0).IsRequired();
        builder.Property(product => product.LowStockThreshold).IsRequired();
        builder.Property(product => product.IsActive).HasDefaultValue(true);
        builder.Property(product => product.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Property(product => product.CreatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(product => product.UpdatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.HasIndex(product => product.NormalizedSku).IsUnique();
        builder.HasMany(product => product.StockMovements)
            .WithOne(movement => movement.Product)
            .HasForeignKey(movement => movement.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(product => product.SalesOrderReservations)
            .WithOne(reservation => reservation.Product)
            .HasForeignKey(reservation => reservation.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
