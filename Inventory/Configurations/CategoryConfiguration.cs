using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", table => table.HasCheckConstraint(
            "CK_Categories_Name_NotBlank",
            "ISNULL(LEN(LTRIM(RTRIM([Name]))), 0) > 0"));
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Name).HasMaxLength(120).IsRequired();
        builder.Property(category => category.NormalizedName).HasMaxLength(120).IsRequired();
        builder.Property(category => category.IsActive).HasDefaultValue(true);
        builder.Property(category => category.CreatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(category => category.UpdatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.HasIndex(category => category.NormalizedName).IsUnique();
        builder.HasMany(category => category.Products)
            .WithOne(product => product.Category)
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
