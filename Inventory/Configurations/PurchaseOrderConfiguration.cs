using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders", table => table.HasCheckConstraint(
            "CK_PurchaseOrders_Status_Valid",
            "[Status] IN (1, 2, 3, 4, 5, 6)"));

        builder.HasKey(order => order.Id);
        builder.Property(order => order.OrderNumber)
            .HasMaxLength(32)
            .HasDefaultValueSql("'PO-' + RIGHT('00000000' + CONVERT(varchar(20), NEXT VALUE FOR dbo.PurchaseOrderNumberSequence), 8)")
            .ValueGeneratedOnAdd()
            .IsRequired();
        builder.Property(order => order.SupplierId).IsRequired();
        builder.Property(order => order.Status).HasConversion<int>().IsRequired();
        builder.Property(order => order.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Property(order => order.CreatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(order => order.UpdatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(order => order.SubmittedAtUtc).HasColumnType("datetime2");
        builder.Property(order => order.ApprovedAtUtc).HasColumnType("datetime2");
        builder.Property(order => order.CancelledAtUtc).HasColumnType("datetime2");
        builder.Property(order => order.CancellationReason).HasMaxLength(500);

        builder.HasIndex(order => order.OrderNumber).IsUnique();
        builder.HasIndex(order => new { order.SupplierId, order.Status, order.CreatedAtUtc });
        builder.HasIndex(order => new { order.Status, order.CreatedAtUtc });

        builder.HasOne(order => order.Supplier)
            .WithMany(supplier => supplier.PurchaseOrders)
            .HasForeignKey(order => order.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.CreatedByUser)
            .WithMany()
            .HasForeignKey(order => order.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.SubmittedByUser)
            .WithMany()
            .HasForeignKey(order => order.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.ApprovedByUser)
            .WithMany()
            .HasForeignKey(order => order.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.CancelledByUser)
            .WithMany()
            .HasForeignKey(order => order.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(order => order.Lines)
            .WithOne(line => line.PurchaseOrder)
            .HasForeignKey(line => line.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(order => order.Receipts)
            .WithOne(receipt => receipt.PurchaseOrder)
            .HasForeignKey(receipt => receipt.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
