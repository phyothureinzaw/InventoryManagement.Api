using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class PurchaseReceiptLineConfiguration : IEntityTypeConfiguration<PurchaseReceiptLine>
{
    public void Configure(EntityTypeBuilder<PurchaseReceiptLine> builder)
    {
        builder.ToTable("PurchaseReceiptLines", table =>
        {
            table.HasCheckConstraint("CK_PurchaseReceiptLines_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_PurchaseReceiptLines_ReturnedQuantity_NonNegative", "[ReturnedQuantity] >= 0");
            table.HasCheckConstraint("CK_PurchaseReceiptLines_ReturnedQuantity_NotOverQuantity", "[ReturnedQuantity] <= [Quantity]");
        });

        builder.HasKey(line => line.Id);
        builder.Property(line => line.Quantity).IsRequired();
        builder.Property(line => line.ReturnedQuantity).HasDefaultValue(0).IsRequired();
        builder.Property(line => line.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Property(line => line.StockMovementId).IsRequired();
        builder.HasAlternateKey(line => new { line.Id, line.PurchaseReceiptId, line.PurchaseOrderId })
            .HasName("AK_PurchaseReceiptLines_Id_Receipt_Order");

        builder.HasIndex(line => new { line.PurchaseReceiptId, line.PurchaseOrderLineId }).IsUnique();
        builder.HasIndex(line => line.PurchaseOrderLineId);
        builder.HasIndex(line => line.StockMovementId).IsUnique();

        builder.HasOne(line => line.StockMovement)
            .WithOne(movement => movement.PurchaseReceiptLine)
            .HasForeignKey<PurchaseReceiptLine>(line => line.StockMovementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
