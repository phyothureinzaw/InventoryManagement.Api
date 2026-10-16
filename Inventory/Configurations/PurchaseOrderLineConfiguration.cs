using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("PurchaseOrderLines", table =>
        {
            table.HasCheckConstraint("CK_PurchaseOrderLines_OrderedQuantity_Positive", "[OrderedQuantity] > 0");
            table.HasCheckConstraint("CK_PurchaseOrderLines_ReceivedQuantity_NonNegative", "[ReceivedQuantity] >= 0");
            table.HasCheckConstraint("CK_PurchaseOrderLines_ReceivedQuantity_NotOverOrdered", "[ReceivedQuantity] <= [OrderedQuantity]");
        });

        builder.HasKey(line => line.Id);
        builder.Property(line => line.OrderedQuantity).IsRequired();
        builder.Property(line => line.ReceivedQuantity).HasDefaultValue(0).IsRequired();
        builder.Property(line => line.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Property(line => line.CreatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(line => line.UpdatedAtUtc).HasColumnType("datetime2").IsRequired();

        builder.HasAlternateKey(line => new { line.Id, line.PurchaseOrderId })
            .HasName("AK_PurchaseOrderLines_Id_PurchaseOrderId");
        builder.HasIndex(line => new { line.PurchaseOrderId, line.ProductId }).IsUnique();
        builder.HasIndex(line => line.ProductId);

        builder.HasOne(line => line.Product)
            .WithMany()
            .HasForeignKey(line => line.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(line => line.ReceiptLines)
            .WithOne(receiptLine => receiptLine.PurchaseOrderLine)
            .HasForeignKey(receiptLine => new { receiptLine.PurchaseOrderLineId, receiptLine.PurchaseOrderId })
            .HasPrincipalKey(line => new { line.Id, line.PurchaseOrderId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
