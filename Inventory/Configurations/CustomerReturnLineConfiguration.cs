using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class CustomerReturnLineConfiguration : IEntityTypeConfiguration<CustomerReturnLine>
{
    public void Configure(EntityTypeBuilder<CustomerReturnLine> builder)
    {
        builder.ToTable("CustomerReturnLines", table => table.HasCheckConstraint(
            "CK_CustomerReturnLines_Quantity_Positive", "[Quantity] > 0"));
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Quantity).IsRequired();
        builder.Property(line => line.StockMovementId).IsRequired();
        builder.HasIndex(line => new { line.CustomerReturnId, line.SalesFulfillmentLineId }).IsUnique();
        builder.HasIndex(line => new { line.SalesFulfillmentId, line.SalesOrderId, line.SalesFulfillmentLineId });
        builder.HasIndex(line => line.StockMovementId).IsUnique();
        builder.HasOne(line => line.SalesFulfillmentLine).WithMany(sourceLine => sourceLine.CustomerReturnLines)
            .HasForeignKey(line => new { line.SalesFulfillmentLineId, line.SalesFulfillmentId, line.SalesOrderId })
            .HasPrincipalKey(sourceLine => new { sourceLine.Id, sourceLine.SalesFulfillmentId, sourceLine.SalesOrderId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(line => line.StockMovement).WithOne(movement => movement.CustomerReturnLine)
            .HasForeignKey<CustomerReturnLine>(line => line.StockMovementId).OnDelete(DeleteBehavior.Restrict);
    }
}
