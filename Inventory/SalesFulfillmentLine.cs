namespace InventoryManagement.Api.Inventory;

public sealed class SalesFulfillmentLine
{
    public Guid Id { get; set; }
    public Guid SalesFulfillmentId { get; set; }
    public Guid SalesOrderId { get; set; }
    public Guid SalesOrderLineId { get; set; }
    public int Quantity { get; set; }
    public int ReturnedQuantity { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Guid StockMovementId { get; set; }

    public SalesFulfillment SalesFulfillment { get; set; } = null!;
    public SalesOrderLine SalesOrderLine { get; set; } = null!;
    public StockMovement StockMovement { get; set; } = null!;
    public ICollection<CustomerReturnLine> CustomerReturnLines { get; set; } = [];
}
