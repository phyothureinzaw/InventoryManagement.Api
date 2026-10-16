using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InventoryManagement.Api.Inventory.Configurations;

public sealed class CustomerReturnConfiguration : IEntityTypeConfiguration<CustomerReturn>
{
    public void Configure(EntityTypeBuilder<CustomerReturn> builder)
    {
        builder.ToTable("CustomerReturns", table => table.HasCheckConstraint(
            "CK_CustomerReturns_Reason_NotBlank",
            "ISNULL(LEN(LTRIM(RTRIM([Reason]))), 0) > 0"));
        builder.HasKey(returnRequest => returnRequest.Id);
        builder.Property(returnRequest => returnRequest.ReturnNumber)
            .HasMaxLength(32)
            .HasDefaultValueSql("'CR-' + RIGHT('00000000' + CONVERT(varchar(20), NEXT VALUE FOR dbo.CustomerReturnNumberSequence), 8)")
            .ValueGeneratedOnAdd()
            .IsRequired();
        builder.Property(returnRequest => returnRequest.RequestId).IsRequired();
        builder.Property(returnRequest => returnRequest.Reason).HasMaxLength(500).IsRequired();
        builder.Property(returnRequest => returnRequest.ReferenceNumber).HasMaxLength(100);
        builder.Property(returnRequest => returnRequest.Notes).HasMaxLength(2000);
        builder.Property(returnRequest => returnRequest.ReturnedAtUtc).HasColumnType("datetime2").IsRequired();
        builder.HasAlternateKey(returnRequest => new { returnRequest.Id, returnRequest.SalesFulfillmentId, returnRequest.SalesOrderId })
            .HasName("AK_CustomerReturns_Id_Fulfillment_Order");
        builder.HasIndex(returnRequest => returnRequest.ReturnNumber).IsUnique();
        builder.HasIndex(returnRequest => returnRequest.RequestId).IsUnique();
        builder.HasIndex(returnRequest => new { returnRequest.SalesFulfillmentId, returnRequest.ReturnedAtUtc });
        builder.HasOne(returnRequest => returnRequest.SalesFulfillment).WithMany(fulfillment => fulfillment.CustomerReturns)
            .HasForeignKey(returnRequest => new { returnRequest.SalesFulfillmentId, returnRequest.SalesOrderId })
            .HasPrincipalKey(fulfillment => new { fulfillment.Id, fulfillment.SalesOrderId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(returnRequest => returnRequest.SalesOrder).WithMany(order => order.CustomerReturns)
            .HasForeignKey(returnRequest => returnRequest.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(returnRequest => returnRequest.Customer).WithMany()
            .HasForeignKey(returnRequest => returnRequest.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(returnRequest => returnRequest.ReturnedByUser).WithMany()
            .HasForeignKey(returnRequest => returnRequest.ReturnedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(returnRequest => returnRequest.Lines).WithOne(line => line.CustomerReturn)
            .HasForeignKey(line => line.CustomerReturnId).OnDelete(DeleteBehavior.Restrict);
    }
}
