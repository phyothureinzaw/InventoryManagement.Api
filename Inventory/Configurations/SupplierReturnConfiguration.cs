using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class SupplierReturnConfiguration : IEntityTypeConfiguration<SupplierReturn>
{
    public void Configure(EntityTypeBuilder<SupplierReturn> builder)
    {
        builder.ToTable("SupplierReturns", table => table.HasCheckConstraint(
            "CK_SupplierReturns_Reason_NotBlank",
            "ISNULL(LEN(LTRIM(RTRIM([Reason]))), 0) > 0"));
        builder.HasKey(returnRequest => returnRequest.Id);
        builder.Property(returnRequest => returnRequest.ReturnNumber)
            .HasMaxLength(32)
            .HasDefaultValueSql("'SR-' + RIGHT('00000000' + CONVERT(varchar(20), NEXT VALUE FOR dbo.SupplierReturnNumberSequence), 8)")
            .ValueGeneratedOnAdd()
            .IsRequired();
        builder.Property(returnRequest => returnRequest.RequestId).IsRequired();
        builder.Property(returnRequest => returnRequest.Reason).HasMaxLength(500).IsRequired();
        builder.Property(returnRequest => returnRequest.ReferenceNumber).HasMaxLength(100);
        builder.Property(returnRequest => returnRequest.Notes).HasMaxLength(2000);
        builder.Property(returnRequest => returnRequest.ReturnedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.HasAlternateKey(returnRequest => new { returnRequest.Id, returnRequest.PurchaseReceiptId, returnRequest.PurchaseOrderId })
            .HasName("AK_SupplierReturns_Id_Receipt_Order");
        builder.HasIndex(returnRequest => returnRequest.ReturnNumber).IsUnique();
        builder.HasIndex(returnRequest => returnRequest.RequestId).IsUnique();
        builder.HasIndex(returnRequest => new { returnRequest.PurchaseReceiptId, returnRequest.ReturnedAtUtc });
        builder.HasOne(returnRequest => returnRequest.PurchaseReceipt).WithMany(receipt => receipt.SupplierReturns)
            .HasForeignKey(returnRequest => new { returnRequest.PurchaseReceiptId, returnRequest.PurchaseOrderId })
            .HasPrincipalKey(receipt => new { receipt.Id, receipt.PurchaseOrderId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(returnRequest => returnRequest.PurchaseOrder).WithMany(order => order.SupplierReturns)
            .HasForeignKey(returnRequest => returnRequest.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(returnRequest => returnRequest.Supplier).WithMany()
            .HasForeignKey(returnRequest => returnRequest.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(returnRequest => returnRequest.ReturnedByUser).WithMany()
            .HasForeignKey(returnRequest => returnRequest.ReturnedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(returnRequest => returnRequest.Lines).WithOne(line => line.SupplierReturn)
            .HasForeignKey(line => line.SupplierReturnId).OnDelete(DeleteBehavior.Restrict);
    }
}
