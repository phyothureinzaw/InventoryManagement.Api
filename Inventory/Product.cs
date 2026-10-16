namespace InventoryManagement.Api.Inventory;

public sealed class Product
{
    public Guid Id { get; set; }
    public required string Sku { get; set; }
    public required string NormalizedSku { get; set; }
    public required string Name { get; set; }
    public Guid CategoryId { get; set; }
    public required string Unit { get; set; }
    public int CurrentStock { get; set; }
    public int ReservedStock { get; set; }
    public int LowStockThreshold { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Category Category { get; set; } = null!;
    public ICollection<StockMovement> StockMovements { get; set; } = [];
    public ICollection<SalesOrderReservation> SalesOrderReservations { get; set; } = [];
}
