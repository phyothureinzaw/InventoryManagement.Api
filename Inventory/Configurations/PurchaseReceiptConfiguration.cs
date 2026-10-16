using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class PurchaseReceiptConfiguration : IEntityTypeConfiguration<PurchaseReceipt>
{
    public void Configure(EntityTypeBuilder<PurchaseReceipt> builder)
    {
        builder.ToTable("PurchaseReceipts");
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.RequestId).IsRequired();
        builder.Property(receipt => receipt.ReceivedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(receipt => receipt.ReferenceNumber).HasMaxLength(100);
        builder.Property(receipt => receipt.Notes).HasMaxLength(2000);

        builder.HasAlternateKey(receipt => new { receipt.Id, receipt.PurchaseOrderId })
            .HasName("AK_PurchaseReceipts_Id_PurchaseOrderId");
        builder.HasIndex(receipt => receipt.RequestId).IsUnique();
        builder.HasIndex(receipt => new { receipt.PurchaseOrderId, receipt.ReceivedAtUtc });

        builder.HasOne(receipt => receipt.ReceivedByUser)
            .WithMany()
            .HasForeignKey(receipt => receipt.ReceivedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(receipt => receipt.Lines)
            .WithOne(line => line.PurchaseReceipt)
            .HasForeignKey(line => new { line.PurchaseReceiptId, line.PurchaseOrderId })
            .HasPrincipalKey(receipt => new { receipt.Id, receipt.PurchaseOrderId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
