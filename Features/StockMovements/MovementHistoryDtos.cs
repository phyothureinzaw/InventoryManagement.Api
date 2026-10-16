using InventoryManagement.Api.Inventory;
using InventoryManagement.Api.Features.Inventory;

namespace InventoryManagement.Api.Features.StockMovements;

public sealed class MovementHistoryQuery : PaginationQuery
{
    public Guid? ProductId { get; init; }
    public string? MovementType { get; init; }
    public Guid? PerformedByUserId { get; init; }
    public string? FromUtc { get; init; }
    public string? ToUtc { get; init; }
    public string? Search { get; init; }
}

public sealed class ActorQuery : PaginationQuery
{
    public string? Search { get; init; }
}

public sealed class DashboardQuery
{
    public string? FromUtc { get; init; }
    public string? ToUtc { get; init; }
}

public sealed record MovementHistoryResponse(
    Guid Id,
    Guid RequestId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string ProductUnit,
    MovementType MovementType,
    int Quantity,
    int StockBefore,
    int StockAfter,
    Guid? SupplierId,
    string? SupplierName,
    string? Reason,
    string? ReferenceNumber,
    string? Notes,
    Guid PerformedByUserId,
    string PerformedByDisplayName,
    DateTime OccurredAtUtc,
    Guid? PurchaseOrderId,
    string? PurchaseOrderNumber,
    Guid? PurchaseReceiptId,
    Guid? SalesOrderId,
    string? SalesOrderNumber,
    Guid? SalesFulfillmentId,
    string? ReturnKind,
    Guid? CustomerReturnId,
    string? CustomerReturnNumber,
    Guid? SupplierReturnId,
    string? SupplierReturnNumber);

public sealed record ActorResponse(Guid Id, string DisplayName);

public sealed record DailyActivityResponse(DateOnly Date, int StockInCount, int StockOutCount);

public sealed record DashboardResponse(
    int ActiveProductCount,
    int LowStockActiveProductCount,
    int OutOfStockActiveProductCount,
    IReadOnlyList<MovementHistoryResponse> LatestMovements,
    IReadOnlyList<DailyActivityResponse> DailyActivity);

public sealed record UtcRange(DateTimeOffset FromUtc, DateTimeOffset ToUtc);

public sealed record HistoryServiceError(string Code, string Detail);
