namespace InventoryManagement.Api.Inventory;

public enum PurchaseOrderStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    PartiallyReceived = 4,
    Received = 5,
    Cancelled = 6,
}
