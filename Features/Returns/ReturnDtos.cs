using System.ComponentModel.DataAnnotations;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Inventory;

namespace InventoryManagement.Api.Features.Returns;

public sealed class CustomerReturnQuery : PaginationQuery
{
    public string? Search { get; init; }
    public string? CustomerSearch { get; init; }
    public Guid? SalesOrderId { get; init; }
    public Guid? SalesFulfillmentId { get; init; }
    public Guid? CustomerId { get; init; }
    public string? FromUtc { get; init; }
    public string? ToUtc { get; init; }
}

public sealed class SupplierReturnQuery : PaginationQuery
{
    public string? Search { get; init; }
    public string? SupplierSearch { get; init; }
    public Guid? PurchaseOrderId { get; init; }
    public Guid? PurchaseReceiptId { get; init; }
    public Guid? SupplierId { get; init; }
    public string? FromUtc { get; init; }
    public string? ToUtc { get; init; }
}

public sealed class CustomerReturnSourceQuery : PaginationQuery
{
    public Guid? SalesFulfillmentId { get; init; }
    public string? Search { get; init; }
    public Guid? SalesOrderId { get; init; }
    public Guid? CustomerId { get; init; }
    public string? FromUtc { get; init; }
    public string? ToUtc { get; init; }
}

public sealed class SupplierReturnSourceQuery : PaginationQuery
{
    public Guid? PurchaseReceiptId { get; init; }
    public string? Search { get; init; }
    public Guid? PurchaseOrderId { get; init; }
    public Guid? SupplierId { get; init; }
    public string? FromUtc { get; init; }
    public string? ToUtc { get; init; }
}

public sealed class CustomerReturnLineRequest
{
    [Required]
    public Guid SalesFulfillmentLineId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}

public sealed class CustomerReturnCreateRequest
{
    [Required]
    public Guid RequestId { get; init; }

    [Required]
    public Guid SalesFulfillmentId { get; init; }

    [Required, StringLength(500)]
    public string Reason { get; init; } = string.Empty;

    [StringLength(100)]
    public string? ReferenceNumber { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }

    [Required]
    public IReadOnlyList<CustomerReturnLineRequest> Lines { get; init; } = [];
}

public sealed class SupplierReturnLineRequest
{
    [Required]
    public Guid PurchaseReceiptLineId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}

public sealed class SupplierReturnCreateRequest
{
    [Required]
    public Guid RequestId { get; init; }

    [Required]
    public Guid PurchaseReceiptId { get; init; }

    [Required, StringLength(500)]
    public string Reason { get; init; } = string.Empty;

    [StringLength(100)]
    public string? ReferenceNumber { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }

    [Required]
    public IReadOnlyList<SupplierReturnLineRequest> Lines { get; init; } = [];
}

public sealed record CustomerReturnSummaryResponse(
    Guid Id,
    string ReturnNumber,
    Guid RequestId,
    Guid SalesOrderId,
    string SalesOrderNumber,
    Guid SalesFulfillmentId,
    Guid CustomerId,
    string CustomerName,
    Guid ReturnedByUserId,
    string ReturnedByDisplayName,
    DateTime ReturnedAtUtc,
    string Reason,
    string? ReferenceNumber,
    string? Notes,
    int LineCount,
    int TotalQuantity);

public sealed record SupplierReturnSummaryResponse(
    Guid Id,
    string ReturnNumber,
    Guid RequestId,
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    Guid PurchaseReceiptId,
    Guid SupplierId,
    string SupplierName,
    Guid ReturnedByUserId,
    string ReturnedByDisplayName,
    DateTime ReturnedAtUtc,
    string Reason,
    string? ReferenceNumber,
    string? Notes,
    int LineCount,
    int TotalQuantity);

public sealed record CustomerReturnSourceLineResponse(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string ProductUnit,
    int Quantity,
    int ReturnedQuantity,
    int ReturnableQuantity,
    int CurrentStock,
    int ReservedStock,
    int AvailableStock);

public sealed record SupplierReturnSourceLineResponse(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string ProductUnit,
    int Quantity,
    int ReturnedQuantity,
    int ReturnableQuantity,
    int CurrentStock,
    int ReservedStock,
    int AvailableStock);

public sealed record CustomerReturnSourceResponse(
    Guid Id,
    Guid SalesOrderId,
    string SalesOrderNumber,
    SalesOrderStatus SalesOrderStatus,
    Guid CustomerId,
    string CustomerName,
    bool CustomerIsActive,
    Guid FulfilledByUserId,
    string FulfilledByDisplayName,
    DateTime FulfilledAtUtc,
    string? ReferenceNumber,
    IReadOnlyList<CustomerReturnSourceLineResponse> Lines);

public sealed record SupplierReturnSourceResponse(
    Guid Id,
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    PurchaseOrderStatus PurchaseOrderStatus,
    Guid SupplierId,
    string SupplierName,
    bool SupplierIsActive,
    Guid ReceivedByUserId,
    string ReceivedByDisplayName,
    DateTime ReceivedAtUtc,
    string? ReferenceNumber,
    IReadOnlyList<SupplierReturnSourceLineResponse> Lines);

public sealed record CustomerReturnLineResponse(
    Guid Id,
    Guid SalesFulfillmentLineId,
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
    int AvailableStock);

public sealed record SupplierReturnLineResponse(
    Guid Id,
    Guid PurchaseReceiptLineId,
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
    int AvailableStock);

public sealed record CustomerReturnDetailResponse(
    Guid Id,
    string ReturnNumber,
    Guid RequestId,
    Guid SalesOrderId,
    string SalesOrderNumber,
    SalesOrderStatus SalesOrderStatus,
    Guid SalesFulfillmentId,
    Guid CustomerId,
    string CustomerName,
    bool CustomerIsActive,
    Guid ReturnedByUserId,
    string ReturnedByDisplayName,
    DateTime ReturnedAtUtc,
    string Reason,
    string? ReferenceNumber,
    string? Notes,
    IReadOnlyList<CustomerReturnLineResponse> Lines);

public sealed record SupplierReturnDetailResponse(
    Guid Id,
    string ReturnNumber,
    Guid RequestId,
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    PurchaseOrderStatus PurchaseOrderStatus,
    Guid PurchaseReceiptId,
    Guid SupplierId,
    string SupplierName,
    bool SupplierIsActive,
    Guid ReturnedByUserId,
    string ReturnedByDisplayName,
    DateTime ReturnedAtUtc,
    string Reason,
    string? ReferenceNumber,
    string? Notes,
    IReadOnlyList<SupplierReturnLineResponse> Lines);

public sealed record ReturnServiceError(string Code, string Detail);
