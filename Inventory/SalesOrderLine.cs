namespace InventoryManagement.Api.Inventory;

public sealed class SalesOrderLine
{
    public Guid Id { get; set; }
    public Guid SalesOrderId { get; set; }
    public Guid ProductId { get; set; }
    public int OrderedQuantity { get; set; }
    public int FulfilledQuantity { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public SalesOrder SalesOrder { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public SalesOrderReservation Reservation { get; set; } = null!;
    public ICollection<SalesFulfillmentLine> FulfillmentLines { get; set; } = [];
}
