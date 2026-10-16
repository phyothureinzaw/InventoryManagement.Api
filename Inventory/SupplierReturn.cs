using InventoryManagement.Api.Identity;

namespace InventoryManagement.Api.Inventory;

public sealed class SupplierReturn
{
    public Guid Id { get; set; }
    public string ReturnNumber { get; set; } = null!;
    public Guid RequestId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid PurchaseReceiptId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid ReturnedByUserId { get; set; }
    public DateTime ReturnedAtUtc { get; set; }
    public required string Reason { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public PurchaseReceipt PurchaseReceipt { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
    public ApplicationUser ReturnedByUser { get; set; } = null!;
    public ICollection<SupplierReturnLine> Lines { get; set; } = [];
}
