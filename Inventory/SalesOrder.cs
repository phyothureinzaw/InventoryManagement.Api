using InventoryManagement.Api.Identity;

namespace InventoryManagement.Api.Inventory;

public sealed class SalesOrder
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;
    public byte[] RowVersion { get; set; } = [];
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? ConfirmedByUserId { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Customer Customer { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ApplicationUser? ConfirmedByUser { get; set; }
    public ApplicationUser? CancelledByUser { get; set; }
    public ICollection<SalesOrderLine> Lines { get; set; } = [];
    public ICollection<SalesFulfillment> Fulfillments { get; set; } = [];
    public ICollection<SalesOrderReservation> Reservations { get; set; } = [];
    public ICollection<CustomerReturn> CustomerReturns { get; set; } = [];
}
