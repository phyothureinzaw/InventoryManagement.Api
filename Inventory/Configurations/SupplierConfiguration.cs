using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers", table => table.HasCheckConstraint(
            "CK_Suppliers_Name_NotBlank",
            "ISNULL(LEN(LTRIM(RTRIM([Name]))), 0) > 0"));
        builder.HasKey(supplier => supplier.Id);
        builder.Property(supplier => supplier.Name).HasMaxLength(200).IsRequired();
        builder.Property(supplier => supplier.ContactName).HasMaxLength(120);
        builder.Property(supplier => supplier.Phone).HasMaxLength(40);
        builder.Property(supplier => supplier.Email).HasMaxLength(320);
        builder.Property(supplier => supplier.Address).HasMaxLength(500);
        builder.Property(supplier => supplier.IsActive).HasDefaultValue(true);
        builder.Property(supplier => supplier.CreatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(supplier => supplier.UpdatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.HasMany(supplier => supplier.StockMovements)
            .WithOne(movement => movement.Supplier)
            .HasForeignKey(movement => movement.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
