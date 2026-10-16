using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(customer => customer.Id);
        builder.Property(customer => customer.Name).HasMaxLength(200).IsRequired();
        builder.Property(customer => customer.ContactName).HasMaxLength(200);
        builder.Property(customer => customer.Email).HasMaxLength(256);
        builder.Property(customer => customer.Phone).HasMaxLength(32);
        builder.Property(customer => customer.Address).HasMaxLength(500);
        builder.Property(customer => customer.IsActive).HasDefaultValue(true).IsRequired();
        builder.Property(customer => customer.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Property(customer => customer.CreatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(customer => customer.UpdatedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.HasIndex(customer => new { customer.IsActive, customer.Name, customer.Id });
        builder.HasMany(customer => customer.SalesOrders)
            .WithOne(order => order.Customer)
            .HasForeignKey(order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
