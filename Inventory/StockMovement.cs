using InventoryManagement.Api.Identity;

namespace InventoryManagement.Api.Inventory;

public sealed class StockMovement
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public MovementType MovementType { get; set; }
    public int Quantity { get; set; }
    public int StockBefore { get; set; }
    public int StockAfter { get; set; }
    public Guid? SupplierId { get; set; }
    public string? Reason { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public Guid PerformedByUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public Guid RequestId { get; set; }

    public Product Product { get; set; } = null!;
    public Supplier? Supplier { get; set; }
    public ApplicationUser PerformedByUser { get; set; } = null!;
    public PurchaseReceiptLine? PurchaseReceiptLine { get; set; }
    public SalesFulfillmentLine? SalesFulfillmentLine { get; set; }
    public CustomerReturnLine? CustomerReturnLine { get; set; }
    public SupplierReturnLine? SupplierReturnLine { get; set; }
}
