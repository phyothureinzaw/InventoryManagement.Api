using InventoryManagement.Api.Identity;

namespace InventoryManagement.Api.Inventory;

public sealed class Customer
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<SalesOrder> SalesOrders { get; set; } = [];
}
