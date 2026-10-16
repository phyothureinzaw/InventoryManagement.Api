using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("SalesOrders", table => table.HasCheckConstraint("CK_SalesOrders_Status_Valid", "[Status] IN (1, 2, 3, 4, 5)"));
        builder.HasKey(order => order.Id);
        builder.Property(order => order.OrderNumber).HasMaxLength(32)
            .HasDefaultValueSql("'SO-' + RIGHT('00000000' + CONVERT(varchar(20), NEXT VALUE FOR dbo.SalesOrderNumberSequence), 8)")
            .ValueGeneratedOnAdd().IsRequired();
        builder.Property(order => order.Status).HasConversion<int>().IsRequired();
        builder.Property(order => order.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Property(order => order.CreatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(order => order.ConfirmedAtUtc).HasColumnType("datetime2");
        builder.Property(order => order.CancelledAtUtc).HasColumnType("datetime2");
        builder.Property(order => order.UpdatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(order => order.CancellationReason).HasMaxLength(500);
        builder.HasIndex(order => order.OrderNumber).IsUnique();
        builder.HasIndex(order => new { order.CustomerId, order.Status, order.CreatedAtUtc });
        builder.HasIndex(order => new { order.Status, order.CreatedAtUtc });
        builder.HasOne(order => order.CreatedByUser).WithMany().HasForeignKey(order => order.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.ConfirmedByUser).WithMany().HasForeignKey(order => order.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.CancelledByUser).WithMany().HasForeignKey(order => order.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(order => order.Lines).WithOne(line => line.SalesOrder).HasForeignKey(line => line.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(order => order.Fulfillments).WithOne(fulfillment => fulfillment.SalesOrder).HasForeignKey(fulfillment => fulfillment.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(order => order.Reservations).WithOne(reservation => reservation.SalesOrder).HasForeignKey(reservation => reservation.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
