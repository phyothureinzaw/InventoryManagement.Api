namespace InventoryManagement.Api.Inventory;

public sealed class SupplierReturnLine
{
    public Guid Id { get; set; }
    public Guid SupplierReturnId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid PurchaseReceiptId { get; set; }
    public Guid PurchaseReceiptLineId { get; set; }
    public int Quantity { get; set; }
    public Guid StockMovementId { get; set; }

    public SupplierReturn SupplierReturn { get; set; } = null!;
    public PurchaseReceiptLine PurchaseReceiptLine { get; set; } = null!;
    public StockMovement StockMovement { get; set; } = null!;
}
