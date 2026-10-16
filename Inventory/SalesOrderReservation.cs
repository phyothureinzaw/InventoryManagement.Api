namespace InventoryManagement.Api.Inventory;

public sealed class SalesOrderReservation
{
    public Guid Id { get; set; }
    public Guid SalesOrderId { get; set; }
    public Guid SalesOrderLineId { get; set; }
    public Guid ProductId { get; set; }
    public int ReservedQuantity { get; set; }
    public int FulfilledQuantity { get; set; }
    public int ReleasedQuantity { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public SalesOrder SalesOrder { get; set; } = null!;
    public SalesOrderLine SalesOrderLine { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
