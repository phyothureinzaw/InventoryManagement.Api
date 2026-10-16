using InventoryManagement.Api.Identity;

namespace InventoryManagement.Api.Inventory;

public sealed class SalesFulfillment
{
    public Guid Id { get; set; }
    public Guid SalesOrderId { get; set; }
    public Guid RequestId { get; set; }
    public Guid FulfilledByUserId { get; set; }
    public DateTime FulfilledAtUtc { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }

    public SalesOrder SalesOrder { get; set; } = null!;
    public ApplicationUser FulfilledByUser { get; set; } = null!;
    public ICollection<SalesFulfillmentLine> Lines { get; set; } = [];
    public ICollection<CustomerReturn> CustomerReturns { get; set; } = [];
}
