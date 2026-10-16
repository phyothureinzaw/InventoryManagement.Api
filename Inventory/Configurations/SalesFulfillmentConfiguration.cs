using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class SalesFulfillmentConfiguration : IEntityTypeConfiguration<SalesFulfillment>
{
    public void Configure(EntityTypeBuilder<SalesFulfillment> builder)
    {
        builder.ToTable("SalesFulfillments");
        builder.HasKey(fulfillment => fulfillment.Id);
        builder.Property(fulfillment => fulfillment.RequestId).IsRequired();
        builder.Property(fulfillment => fulfillment.FulfilledAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(fulfillment => fulfillment.ReferenceNumber).HasMaxLength(100);
        builder.Property(fulfillment => fulfillment.Notes).HasMaxLength(2000);
        builder.HasAlternateKey(fulfillment => new { fulfillment.Id, fulfillment.SalesOrderId }).HasName("AK_SalesFulfillments_Id_SalesOrderId");
        builder.HasIndex(fulfillment => fulfillment.RequestId).IsUnique();
        builder.HasIndex(fulfillment => new { fulfillment.SalesOrderId, fulfillment.FulfilledAtUtc });
        builder.HasOne(fulfillment => fulfillment.FulfilledByUser).WithMany().HasForeignKey(fulfillment => fulfillment.FulfilledByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(fulfillment => fulfillment.Lines).WithOne(line => line.SalesFulfillment)
            .HasForeignKey(line => new { line.SalesFulfillmentId, line.SalesOrderId })
            .HasPrincipalKey(fulfillment => new { fulfillment.Id, fulfillment.SalesOrderId }).OnDelete(DeleteBehavior.Restrict);
    }
}
