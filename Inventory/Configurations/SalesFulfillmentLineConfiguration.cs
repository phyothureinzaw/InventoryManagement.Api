using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class SalesFulfillmentLineConfiguration : IEntityTypeConfiguration<SalesFulfillmentLine>
{
    public void Configure(EntityTypeBuilder<SalesFulfillmentLine> builder)
    {
        builder.ToTable("SalesFulfillmentLines", table =>
        {
            table.HasCheckConstraint("CK_SalesFulfillmentLines_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_SalesFulfillmentLines_ReturnedQuantity_NonNegative", "[ReturnedQuantity] >= 0");
            table.HasCheckConstraint("CK_SalesFulfillmentLines_ReturnedQuantity_NotOverQuantity", "[ReturnedQuantity] <= [Quantity]");
        });
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Quantity).IsRequired();
        builder.Property(line => line.ReturnedQuantity).HasDefaultValue(0).IsRequired();
        builder.Property(line => line.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Property(line => line.StockMovementId).IsRequired();
        builder.HasAlternateKey(line => new { line.Id, line.SalesFulfillmentId, line.SalesOrderId })
            .HasName("AK_SalesFulfillmentLines_Id_Fulfillment_Order");
        builder.HasIndex(line => new { line.SalesFulfillmentId, line.SalesOrderLineId }).IsUnique();
        builder.HasIndex(line => line.SalesOrderLineId);
        builder.HasIndex(line => line.StockMovementId).IsUnique();
        builder.HasOne(line => line.StockMovement).WithOne(movement => movement.SalesFulfillmentLine)
            .HasForeignKey<SalesFulfillmentLine>(line => line.StockMovementId).OnDelete(DeleteBehavior.Restrict);
    }
}
