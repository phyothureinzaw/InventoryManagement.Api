using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Api.Features.Inventory;

public sealed class CategoryWriteRequest
{
    [Required, StringLength(120)]
    public string Name { get; init; } = string.Empty;
}

public sealed class SupplierWriteRequest
{
    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(120)]
    public string? ContactName { get; init; }

    [StringLength(40)]
    public string? Phone { get; init; }

    [StringLength(320), EmailAddress]
    public string? Email { get; init; }

    [StringLength(500)]
    public string? Address { get; init; }
}

public sealed class ProductWriteRequest
{
    [Required, StringLength(64)]
    public string Sku { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public Guid CategoryId { get; init; }

    [Required, StringLength(32)]
    public string Unit { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int LowStockThreshold { get; init; }

    public string? RowVersion { get; init; }
}

public sealed class StatusRequest
{
    public bool? IsActive { get; init; }
}

public sealed class ProductStatusRequest
{
    public bool? IsActive { get; init; }

    [Required]
    public string RowVersion { get; init; } = string.Empty;
}

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record SupplierResponse(
    Guid Id,
    string Name,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    Guid CategoryId,
    string CategoryName,
    string Unit,
    int CurrentStock,
    int ReservedStock,
    int AvailableStock,
    int LowStockThreshold,
    bool IsLowStock,
    bool IsOutOfStock,
    bool IsActive,
    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public class PaginationQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class CategoryQuery : PaginationQuery
{
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class SupplierQuery : PaginationQuery
{
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class ProductQuery : PaginationQuery
{
    public string? Search { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsActive { get; init; }
    public string StockStatus { get; init; } = "all";
}

public sealed record ServiceError(string Code, string Detail);
