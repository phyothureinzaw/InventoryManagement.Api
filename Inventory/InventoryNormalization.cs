namespace InventoryManagement.Api.Inventory;

public static class InventoryNormalization
{
    public static string TrimAndUpperInvariant(string value) => value.Trim().ToUpperInvariant();

    public static string NormalizeSku(string sku) => TrimAndUpperInvariant(sku);

    public static string NormalizeCategoryName(string name) => TrimAndUpperInvariant(name);
}
