using InventoryManagement.Api.Identity;

namespace InventoryManagement.Api.Inventory;

public sealed class CustomerReturn
{
    public Guid Id { get; set; }
    public string ReturnNumber { get; set; } = null!;
    public Guid RequestId { get; set; }
    public Guid SalesOrderId { get; set; }
    public Guid SalesFulfillmentId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ReturnedByUserId { get; set; }
    public DateTime ReturnedAtUtc { get; set; }
    public required string Reason { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }

    public SalesOrder SalesOrder { get; set; } = null!;
    public SalesFulfillment SalesFulfillment { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public ApplicationUser ReturnedByUser { get; set; } = null!;
    public ICollection<CustomerReturnLine> Lines { get; set; } = [];
}
