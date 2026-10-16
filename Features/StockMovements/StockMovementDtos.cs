using System.ComponentModel.DataAnnotations;
using InventoryManagement.Api.Inventory;

namespace InventoryManagement.Api.Features.StockMovements;

public sealed class StockInRequest
{
    [Required]
    public Guid ProductId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }

    public Guid? SupplierId { get; init; }

    [StringLength(100)]
    public string? ReferenceNumber { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }

    [Required]
    public Guid RequestId { get; init; }
}

public sealed class StockOutRequest
{
    [Required]
    public Guid ProductId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }

    [Required, StringLength(500)]
    public string Reason { get; init; } = string.Empty;

    [StringLength(100)]
    public string? ReferenceNumber { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }

    [Required]
    public Guid RequestId { get; init; }
}

public sealed record StockMovementResponse(
    Guid Id,
    Guid ProductId,
    MovementType MovementType,
    int Quantity,
    int StockBefore,
    int StockAfter,
    Guid? SupplierId,
    string? Reason,
    string? ReferenceNumber,
    string? Notes,
    Guid PerformedByUserId,
    DateTime OccurredAtUtc,
    Guid RequestId);

public sealed record StockMovementServiceError(string Code, string Detail);
