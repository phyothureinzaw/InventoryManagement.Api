using InventoryManagement.Api.Identity;
using InventoryManagement.Api.Inventory;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Api.Data;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();
        public DbSet<PurchaseReceiptLine> PurchaseReceiptLines => Set<PurchaseReceiptLine>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();
    public DbSet<SalesFulfillment> SalesFulfillments => Set<SalesFulfillment>();
    public DbSet<SalesFulfillmentLine> SalesFulfillmentLines => Set<SalesFulfillmentLine>();
    public DbSet<SalesOrderReservation> SalesOrderReservations => Set<SalesOrderReservation>();
    public DbSet<CustomerReturn> CustomerReturns => Set<CustomerReturn>();
    public DbSet<CustomerReturnLine> CustomerReturnLines => Set<CustomerReturnLine>();
    public DbSet<SupplierReturn> SupplierReturns => Set<SupplierReturn>();
    public DbSet<SupplierReturnLine> SupplierReturnLines => Set<SupplierReturnLine>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasSequence<long>("PurchaseOrderNumberSequence", "dbo")
            .StartsAt(1)
            .IncrementsBy(1);
        builder.HasSequence<long>("SalesOrderNumberSequence", "dbo")
            .StartsAt(1)
            .IncrementsBy(1);
        builder.HasSequence<long>("CustomerReturnNumberSequence", "dbo")
            .StartsAt(1)
            .IncrementsBy(1);
        builder.HasSequence<long>("SupplierReturnNumberSequence", "dbo")
            .StartsAt(1)
            .IncrementsBy(1);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
