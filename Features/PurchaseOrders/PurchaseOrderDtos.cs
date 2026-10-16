using System.ComponentModel.DataAnnotations;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Inventory;

namespace InventoryManagement.Api.Features.PurchaseOrders;

public sealed class PurchaseOrderQuery : PaginationQuery
{
    public string? Search { get; init; }
    public Guid? SupplierId { get; init; }
    public string? Status { get; init; }
    public string? FromUtc { get; init; }
    public string? ToUtc { get; init; }
}

public sealed class PurchaseOrderLineWriteRequest
{
    [Required]
    public Guid ProductId { get; init; }

    [Range(1, int.MaxValue)]
    public int OrderedQuantity { get; init; }
}

public sealed class PurchaseOrderCreateRequest
{
    [Required]
    public Guid SupplierId { get; init; }

    [Required]
    public IReadOnlyList<PurchaseOrderLineWriteRequest> Lines { get; init; } = [];
}

public sealed class PurchaseOrderUpdateRequest
{
    [Required]
    public Guid SupplierId { get; init; }

    [Required]
    public IReadOnlyList<PurchaseOrderLineWriteRequest> Lines { get; init; } = [];

    [Required]
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class PurchaseOrderVersionRequest
{
    [Required]
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class PurchaseOrderCancelRequest
{
    [Required, StringLength(500)]
    public string CancellationReason { get; init; } = string.Empty;

    [Required]
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class PurchaseReceiptQuery : PaginationQuery;

public sealed class PurchaseReceiptLineRequest
{
    [Required]
    public Guid PurchaseOrderLineId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}

public sealed class PurchaseReceiptCreateRequest
{
    [Required]
    public Guid RequestId { get; init; }

    [Required]
    public string RowVersion { get; init; } = string.Empty;

    [StringLength(100)]
    public string? ReferenceNumber { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }

    [Required]
    public IReadOnlyList<PurchaseReceiptLineRequest> Lines { get; init; } = [];
}

public sealed record PurchaseOrderSummaryResponse(
    Guid Id,
    string OrderNumber,
    Guid SupplierId,
    string SupplierName,
    PurchaseOrderStatus Status,
    Guid CreatedByUserId,
    string CreatedByDisplayName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int LineCount,
    int OrderedQuantity,
    int ReceivedQuantity,
    string RowVersion);

public sealed record PurchaseOrderAuditResponse(
    Guid CreatedByUserId,
    string CreatedByDisplayName,
    DateTime CreatedAtUtc,
    Guid? SubmittedByUserId,
    string? SubmittedByDisplayName,
    DateTime? SubmittedAtUtc,
    Guid? ApprovedByUserId,
    string? ApprovedByDisplayName,
    DateTime? ApprovedAtUtc,
    Guid? CancelledByUserId,
    string? CancelledByDisplayName,
    DateTime? CancelledAtUtc,
    string? CancellationReason);

public sealed record PurchaseOrderLineResponse(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string ProductUnit,
    int OrderedQuantity,
    int ReceivedQuantity,
    int RemainingQuantity,
    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record PurchaseOrderDetailResponse(
    Guid Id,
    string OrderNumber,
    Guid SupplierId,
    string SupplierName,
    bool SupplierIsActive,
    PurchaseOrderStatus Status,
    string RowVersion,
    DateTime UpdatedAtUtc,
    PurchaseOrderAuditResponse Audit,
    IReadOnlyList<PurchaseOrderLineResponse> Lines);

public sealed record PurchaseReceiptSummaryResponse(
    Guid Id,
    Guid RequestId,
    Guid ReceivedByUserId,
    string ReceivedByDisplayName,
    DateTime ReceivedAtUtc,
    string? ReferenceNumber,
    string? Notes,
    int LineCount,
    int TotalQuantity);

public sealed record PurchaseReceiptLineResponse(
    Guid Id,
    Guid PurchaseOrderLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string ProductUnit,
    int Quantity,
    Guid StockMovementId,
    int StockBefore,
    int StockAfter,
    int ReturnedQuantity,
    int ReturnableQuantity);

public sealed record PurchaseReceiptDetailResponse(
    Guid Id,
    Guid PurchaseOrderId,
    string OrderNumber,
    PurchaseOrderStatus OrderStatus,
    string OrderRowVersion,
    Guid RequestId,
    Guid ReceivedByUserId,
    string ReceivedByDisplayName,
    DateTime ReceivedAtUtc,
    string? ReferenceNumber,
    string? Notes,
    IReadOnlyList<PurchaseReceiptLineResponse> Lines);

public sealed record PurchaseOrderServiceError(string Code, string Detail);
