using System.ComponentModel.DataAnnotations;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Inventory;

namespace InventoryManagement.Api.Features.SalesOrders;

public sealed class SalesOrderQuery : PaginationQuery
{
    public string? Search { get; init; }
    public Guid? CustomerId { get; init; }
    public string? Status { get; init; }
    public string? FromUtc { get; init; }
    public string? ToUtc { get; init; }
}

public sealed class SalesOrderLineWriteRequest
{
    [Required]
    public Guid ProductId { get; init; }

    [Range(1, int.MaxValue)]
    public int OrderedQuantity { get; init; }
}

public sealed class SalesOrderCreateRequest
{
    [Required]
    public Guid CustomerId { get; init; }

    [Required]
    public IReadOnlyList<SalesOrderLineWriteRequest> Lines { get; init; } = [];
}

public sealed class SalesOrderUpdateRequest
{
    [Required]
    public Guid CustomerId { get; init; }

    [Required]
    public IReadOnlyList<SalesOrderLineWriteRequest> Lines { get; init; } = [];

    [Required]
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class SalesOrderConfirmRequest
{
    [Required]
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class SalesFulfillmentLineRequest
{
    [Required]
    public Guid SalesOrderLineId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}

public sealed class SalesFulfillmentCreateRequest
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
    public IReadOnlyList<SalesFulfillmentLineRequest> Lines { get; init; } = [];
}

public sealed class SalesFulfillmentQuery : PaginationQuery;

public sealed class SalesOrderCancelRequest
{
    [Required, StringLength(500)]
    public string CancellationReason { get; init; } = string.Empty;

    [Required]
    public string RowVersion { get; init; } = string.Empty;
}

public sealed record SalesOrderSummaryResponse(
    Guid Id,
    string OrderNumber,
    Guid CustomerId,
    string CustomerName,
    SalesOrderStatus Status,
    Guid CreatedByUserId,
    string CreatedByDisplayName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int LineCount,
    int OrderedQuantity,
    int FulfilledQuantity,
    string RowVersion);

public sealed record SalesOrderAuditResponse(
    Guid CreatedByUserId,
    string CreatedByDisplayName,
    DateTime CreatedAtUtc,
    Guid? ConfirmedByUserId,
    string? ConfirmedByDisplayName,
    DateTime? ConfirmedAtUtc,
    Guid? CancelledByUserId,
    string? CancelledByDisplayName,
    DateTime? CancelledAtUtc,
    string? CancellationReason);

public sealed record SalesOrderLineResponse(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string ProductUnit,
    bool ProductIsActive,
    int OrderedQuantity,
    int FulfilledQuantity,
    int UnfulfilledQuantity,
    int ActiveReservationQuantity,
    int CurrentStock,
    int ReservedStock,
    int AvailableStock,
    string RowVersion,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record SalesOrderDetailResponse(
    Guid Id,
    string OrderNumber,
    Guid CustomerId,
    string CustomerName,
    bool CustomerIsActive,
    SalesOrderStatus Status,
    string RowVersion,
    DateTime UpdatedAtUtc,
    SalesOrderAuditResponse Audit,
    IReadOnlyList<SalesOrderLineResponse> Lines);

public sealed record SalesFulfillmentSummaryResponse(
    Guid Id,
    Guid RequestId,
    Guid FulfilledByUserId,
    string FulfilledByDisplayName,
    DateTime FulfilledAtUtc,
    string? ReferenceNumber,
    string? Notes,
    int LineCount,
    int TotalQuantity);

public sealed record SalesFulfillmentLineResponse(
    Guid Id,
    Guid SalesOrderLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string ProductUnit,
    int Quantity,
    Guid StockMovementId,
    int StockBefore,
    int StockAfter,
    int CurrentStock,
    int ReservedStock,
    int AvailableStock,
    int ReturnedQuantity,
    int ReturnableQuantity);

public sealed record SalesFulfillmentDetailResponse(
    Guid Id,
    Guid SalesOrderId,
    string OrderNumber,
    SalesOrderStatus OrderStatus,
    string OrderRowVersion,
    Guid RequestId,
    Guid FulfilledByUserId,
    string FulfilledByDisplayName,
    DateTime FulfilledAtUtc,
    string? ReferenceNumber,
    string? Notes,
    IReadOnlyList<SalesFulfillmentLineResponse> Lines);

public sealed record SalesOrderServiceError(string Code, string Detail);
