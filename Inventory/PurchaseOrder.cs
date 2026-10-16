using InventoryManagement.Api.Identity;

namespace InventoryManagement.Api.Inventory;

public sealed class PurchaseOrder
{
    public Guid Id { get; set; }
    public required string OrderNumber { get; set; }
    public Guid SupplierId { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public byte[] RowVersion { get; set; } = [];
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }

    public Supplier Supplier { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? SubmittedByUser { get; set; }
    public ApplicationUser? ApprovedByUser { get; set; }
    public ApplicationUser? CancelledByUser { get; set; }
    public ICollection<PurchaseOrderLine> Lines { get; set; } = [];
    public ICollection<PurchaseReceipt> Receipts { get; set; } = [];
    public ICollection<SupplierReturn> SupplierReturns { get; set; } = [];
}
