using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class SalesOrderReservationConfiguration : IEntityTypeConfiguration<SalesOrderReservation>
{
    public void Configure(EntityTypeBuilder<SalesOrderReservation> builder)
    {
        builder.ToTable("SalesOrderReservations", table =>
        {
            table.HasCheckConstraint("CK_SalesOrderReservations_ReservedQuantity_Positive", "[ReservedQuantity] > 0");
            table.HasCheckConstraint("CK_SalesOrderReservations_FulfilledQuantity_NonNegative", "[FulfilledQuantity] >= 0");
            table.HasCheckConstraint("CK_SalesOrderReservations_ReleasedQuantity_NonNegative", "[ReleasedQuantity] >= 0");
            table.HasCheckConstraint("CK_SalesOrderReservations_ConsumedQuantity_NotOverReserved", "CONVERT(bigint, [FulfilledQuantity]) + CONVERT(bigint, [ReleasedQuantity]) <= CONVERT(bigint, [ReservedQuantity])");
        });
        builder.HasKey(reservation => reservation.Id);
        builder.Property(reservation => reservation.ReservedQuantity).IsRequired();
        builder.Property(reservation => reservation.FulfilledQuantity).HasDefaultValue(0).IsRequired();
        builder.Property(reservation => reservation.ReleasedQuantity).HasDefaultValue(0).IsRequired();
        builder.Property(reservation => reservation.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Property(reservation => reservation.CreatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(reservation => reservation.UpdatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.HasIndex(reservation => new { reservation.SalesOrderId, reservation.SalesOrderLineId }).IsUnique();
        builder.HasIndex(reservation => new { reservation.ProductId, reservation.SalesOrderId });
        builder.HasAlternateKey(reservation => new { reservation.SalesOrderLineId, reservation.SalesOrderId, reservation.ProductId }).HasName("AK_SalesOrderReservations_Line_Order_Product");
        builder.HasOne(reservation => reservation.SalesOrderLine).WithOne(line => line.Reservation)
            .HasForeignKey<SalesOrderReservation>(reservation => new { reservation.SalesOrderLineId, reservation.SalesOrderId, reservation.ProductId })
            .HasPrincipalKey<SalesOrderLine>(line => new { line.Id, line.SalesOrderId, line.ProductId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(reservation => reservation.Product).WithMany(product => product.SalesOrderReservations)
            .HasForeignKey(reservation => reservation.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
