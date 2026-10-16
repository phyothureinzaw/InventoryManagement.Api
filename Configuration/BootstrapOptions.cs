namespace InventoryManagement.Api.Configuration;

public sealed class BootstrapOptions
{
    public bool Enabled { get; init; }
    public string? AdminEmail { get; init; }
    public string? AdminPassword { get; init; }
    public string? AdminDisplayName { get; init; }
}
