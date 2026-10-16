namespace InventoryManagement.Api.Inventory;

public sealed class CustomerReturnLine
{
    public Guid Id { get; set; }
    public Guid CustomerReturnId { get; set; }
    public Guid SalesOrderId { get; set; }
    public Guid SalesFulfillmentId { get; set; }
    public Guid SalesFulfillmentLineId { get; set; }
    public int Quantity { get; set; }
    public Guid StockMovementId { get; set; }

    public CustomerReturn CustomerReturn { get; set; } = null!;
    public SalesFulfillmentLine SalesFulfillmentLine { get; set; } = null!;
    public StockMovement StockMovement { get; set; } = null!;
}
