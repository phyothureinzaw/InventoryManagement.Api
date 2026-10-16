using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements", table =>
        {
            table.HasCheckConstraint("CK_StockMovements_MovementType_Valid", "[MovementType] IN (1, 2)");
            table.HasCheckConstraint("CK_StockMovements_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_StockMovements_StockBefore_NonNegative", "[StockBefore] >= 0");
            table.HasCheckConstraint("CK_StockMovements_StockAfter_NonNegative", "[StockAfter] >= 0");
            table.HasCheckConstraint(
                "CK_StockMovements_StockIn_Balance",
                "[MovementType] <> 1 OR CONVERT(bigint, [StockAfter]) - CONVERT(bigint, [StockBefore]) = CONVERT(bigint, [Quantity])");
            table.HasCheckConstraint(
                "CK_StockMovements_StockOut_Balance",
                "[MovementType] <> 2 OR CONVERT(bigint, [StockBefore]) - CONVERT(bigint, [StockAfter]) = CONVERT(bigint, [Quantity])");
            table.HasCheckConstraint(
                "CK_StockMovements_StockOut_ReasonAndSupplier",
                "[MovementType] <> 2 OR (ISNULL(LEN(LTRIM(RTRIM([Reason]))), 0) > 0 AND [SupplierId] IS NULL)");
        });
        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.MovementType).HasConversion<int>().IsRequired();
        builder.Property(movement => movement.Reason).HasMaxLength(500);
        builder.Property(movement => movement.ReferenceNumber).HasMaxLength(100);
        builder.Property(movement => movement.Notes).HasMaxLength(2000);
        builder.Property(movement => movement.OccurredAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(movement => movement.RequestId).IsRequired();
        builder.HasIndex(movement => movement.RequestId).IsUnique();
        builder.HasIndex(movement => new { movement.ProductId, movement.OccurredAtUtc, movement.Id });
        builder.HasIndex(movement => new { movement.OccurredAtUtc, movement.Id });
        builder.HasOne(movement => movement.PerformedByUser)
            .WithMany()
            .HasForeignKey(movement => movement.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
