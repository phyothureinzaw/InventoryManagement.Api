namespace InventoryManagement.Api.Inventory;

public sealed class PurchaseReceiptLine
{
    public Guid Id { get; set; }
    public Guid PurchaseReceiptId { get; set; }
    // Repeated to enforce that a receipt line and its order line belong to the same order.
    public Guid PurchaseOrderId { get; set; }
    public Guid PurchaseOrderLineId { get; set; }
    public int Quantity { get; set; }
    public int ReturnedQuantity { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Guid StockMovementId { get; set; }

    public PurchaseReceipt PurchaseReceipt { get; set; } = null!;
    public PurchaseOrderLine PurchaseOrderLine { get; set; } = null!;
    public StockMovement StockMovement { get; set; } = null!;
    public ICollection<SupplierReturnLine> SupplierReturnLines { get; set; } = [];
}
