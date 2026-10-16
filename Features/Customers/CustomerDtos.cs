using System.ComponentModel.DataAnnotations;
using InventoryManagement.Api.Features.Inventory;

namespace InventoryManagement.Api.Features.Customers;

public sealed class CustomerQuery : PaginationQuery
{
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
}

public class CustomerWriteRequest
{
    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(200)]
    public string? ContactName { get; init; }

    [StringLength(256), EmailAddress]
    public string? Email { get; init; }

    [StringLength(32)]
    public string? Phone { get; init; }

    [StringLength(500)]
    public string? Address { get; init; }
}

public sealed class CustomerUpdateRequest : CustomerWriteRequest
{
    [Required]
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CustomerStatusRequest
{
    [Required]
    public string RowVersion { get; init; } = string.Empty;
    public bool? IsActive { get; init; }
}

public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    bool IsActive,
    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CustomerServiceError(string Code, string Detail);
