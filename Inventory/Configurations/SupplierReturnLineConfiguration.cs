using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class SupplierReturnLineConfiguration : IEntityTypeConfiguration<SupplierReturnLine>
{
    public void Configure(EntityTypeBuilder<SupplierReturnLine> builder)
    {
        builder.ToTable("SupplierReturnLines", table => table.HasCheckConstraint(
            "CK_SupplierReturnLines_Quantity_Positive", "[Quantity] > 0"));
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Quantity).IsRequired();
        builder.Property(line => line.StockMovementId).IsRequired();
        builder.HasIndex(line => new { line.SupplierReturnId, line.PurchaseReceiptLineId }).IsUnique();
        builder.HasIndex(line => new { line.PurchaseReceiptId, line.PurchaseOrderId, line.PurchaseReceiptLineId });
        builder.HasIndex(line => line.StockMovementId).IsUnique();
        builder.HasOne(line => line.PurchaseReceiptLine).WithMany(sourceLine => sourceLine.SupplierReturnLines)
            .HasForeignKey(line => new { line.PurchaseReceiptLineId, line.PurchaseReceiptId, line.PurchaseOrderId })
            .HasPrincipalKey(sourceLine => new { sourceLine.Id, sourceLine.PurchaseReceiptId, sourceLine.PurchaseOrderId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(line => line.StockMovement).WithOne(movement => movement.SupplierReturnLine)
            .HasForeignKey<SupplierReturnLine>(line => line.StockMovementId).OnDelete(DeleteBehavior.Restrict);
    }
}
