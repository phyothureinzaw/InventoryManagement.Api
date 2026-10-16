using InventoryManagement.Api.Identity;

namespace InventoryManagement.Api.Inventory;

public sealed class PurchaseReceipt
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid RequestId { get; set; }
    public Guid ReceivedByUserId { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public ApplicationUser ReceivedByUser { get; set; } = null!;
    public ICollection<PurchaseReceiptLine> Lines { get; set; } = [];
    public ICollection<SupplierReturn> SupplierReturns { get; set; } = [];
}
