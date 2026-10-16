using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class SalesOrderLineConfiguration : IEntityTypeConfiguration<SalesOrderLine>
{
    public void Configure(EntityTypeBuilder<SalesOrderLine> builder)
    {
        builder.ToTable("SalesOrderLines", table =>
        {
            table.HasCheckConstraint("CK_SalesOrderLines_OrderedQuantity_Positive", "[OrderedQuantity] > 0");
            table.HasCheckConstraint("CK_SalesOrderLines_FulfilledQuantity_NonNegative", "[FulfilledQuantity] >= 0");
            table.HasCheckConstraint("CK_SalesOrderLines_FulfilledQuantity_NotOverOrdered", "[FulfilledQuantity] <= [OrderedQuantity]");
        });
        builder.HasKey(line => line.Id);
        builder.Property(line => line.OrderedQuantity).IsRequired();
        builder.Property(line => line.FulfilledQuantity).HasDefaultValue(0).IsRequired();
        builder.Property(line => line.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Property(line => line.CreatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(line => line.UpdatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.HasAlternateKey(line => new { line.Id, line.SalesOrderId }).HasName("AK_SalesOrderLines_Id_SalesOrderId");
        builder.HasAlternateKey(line => new { line.Id, line.SalesOrderId, line.ProductId }).HasName("AK_SalesOrderLines_Id_SalesOrderId_ProductId");
        builder.HasIndex(line => new { line.SalesOrderId, line.ProductId }).IsUnique();
        builder.HasIndex(line => line.ProductId);
        builder.HasOne(line => line.Product).WithMany().HasForeignKey(line => line.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(line => line.FulfillmentLines).WithOne(fulfillmentLine => fulfillmentLine.SalesOrderLine)
            .HasForeignKey(fulfillmentLine => new { fulfillmentLine.SalesOrderLineId, fulfillmentLine.SalesOrderId })
            .HasPrincipalKey(line => new { line.Id, line.SalesOrderId }).OnDelete(DeleteBehavior.Restrict);
    }
}
