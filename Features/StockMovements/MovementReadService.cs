using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using InventoryManagement.Api.Data;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Inventory;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Api.Features.StockMovements;

public sealed class MovementReadService(IDbContextFactory<ApplicationDbContext> contextFactory)
{
    public async Task<(PagedResponse<MovementHistoryResponse>? Response, HistoryServiceError? Error)> GetAsync(
        MovementHistoryQuery query,
        CancellationToken cancellationToken)
    {
        if (!InventoryServiceSupport.IsValidPagination(query))
            return (null, new("validation", "Page must be at least 1 and pageSize must be between 1 and 100."));
        if (!TryParseMovementType(query.MovementType, out var movementType, out var movementTypeError))
            return (null, movementTypeError);
        if (!UtcRangeParser.TryParseOptional(query.FromUtc, query.ToUtc, out var range, out var rangeError))
            return (null, rangeError);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var movements = ApplyFilters(db.StockMovements.AsNoTracking(), query, movementType, range);
        var total = await movements.CountAsync(cancellationToken);
        var rows = await movements
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToProjection())
            .ToListAsync(cancellationToken);
        var items = rows.Select(ToResponse).ToList();
        return (new PagedResponse<MovementHistoryResponse>(items, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)), null);
    }

    public async Task<MovementHistoryResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.StockMovements.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(ToProjection())
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : ToResponse(row);
    }

    public async Task<IReadOnlyList<MovementHistoryResponse>> GetLatestAsync(int count, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.StockMovements.AsNoTracking()
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(count)
            .Select(ToProjection())
            .ToListAsync(cancellationToken);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<(PagedResponse<ActorResponse>? Response, HistoryServiceError? Error)> GetActorsAsync(
        ActorQuery query,
        CancellationToken cancellationToken)
    {
        if (!InventoryServiceSupport.IsValidPagination(query))
            return (null, new("validation", "Page must be at least 1 and pageSize must be between 1 and 100."));
        var search = query.Search?.Trim();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var actors = db.StockMovements.AsNoTracking()
            .Select(x => new { x.PerformedByUserId, DisplayName = x.PerformedByUser.DisplayName })
            .Distinct();
        if (!string.IsNullOrWhiteSpace(search)) actors = actors.Where(x => x.DisplayName.Contains(search));
        var total = await actors.CountAsync(cancellationToken);
        var items = await actors.OrderBy(x => x.DisplayName).ThenBy(x => x.PerformedByUserId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new ActorResponse(x.PerformedByUserId, x.DisplayName))
            .ToListAsync(cancellationToken);
        return (new PagedResponse<ActorResponse>(items, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)), null);
    }

    private static IQueryable<StockMovement> ApplyFilters(
        IQueryable<StockMovement> movements,
        MovementHistoryQuery query,
        MovementType? movementType,
        UtcRange? range)
    {
        if (query.ProductId.HasValue) movements = movements.Where(x => x.ProductId == query.ProductId.Value);
        if (query.PerformedByUserId.HasValue) movements = movements.Where(x => x.PerformedByUserId == query.PerformedByUserId.Value);
        if (movementType.HasValue) movements = movements.Where(x => x.MovementType == movementType.Value);
        if (range is not null)
        {
            movements = movements.Where(x => x.OccurredAtUtc >= range.FromUtc.UtcDateTime && x.OccurredAtUtc < range.ToUtc.UtcDateTime);
        }
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
            movements = movements.Where(x => x.Product.Sku.Contains(search) || x.Product.Name.Contains(search) || (x.ReferenceNumber != null && x.ReferenceNumber.Contains(search)));
        return movements;
    }

    private static bool TryParseMovementType(string? value, out MovementType? movementType, out HistoryServiceError? error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            movementType = null;
            error = null;
            return true;
        }
        if (Enum.TryParse<MovementType>(value, true, out var parsed) && parsed is MovementType.StockIn or MovementType.StockOut)
        {
            movementType = parsed;
            error = null;
            return true;
        }
        movementType = null;
        error = new("validation", "movementType must be StockIn or StockOut.");
        return false;
    }

    private static Expression<Func<StockMovement, MovementProjection>> ToProjection() => movement => new MovementProjection(
        movement.Id,
        movement.RequestId,
        movement.ProductId,
        movement.Product.Sku,
        movement.Product.Name,
        movement.Product.Unit,
        movement.MovementType,
        movement.Quantity,
        movement.StockBefore,
        movement.StockAfter,
        movement.SupplierId,
        movement.Supplier == null ? null : movement.Supplier.Name,
        movement.Reason,
        movement.ReferenceNumber,
        movement.Notes,
        movement.PerformedByUserId,
        movement.PerformedByUser.DisplayName,
        movement.OccurredAtUtc,
         movement.PurchaseReceiptLine != null ? movement.PurchaseReceiptLine.PurchaseOrderId : movement.SupplierReturnLine == null ? null : movement.SupplierReturnLine.SupplierReturn.PurchaseOrderId,
         movement.PurchaseReceiptLine != null ? movement.PurchaseReceiptLine.PurchaseReceipt.PurchaseOrder.OrderNumber : movement.SupplierReturnLine == null ? null : movement.SupplierReturnLine.SupplierReturn.PurchaseOrder.OrderNumber,
         movement.PurchaseReceiptLine != null ? movement.PurchaseReceiptLine.PurchaseReceiptId : movement.SupplierReturnLine == null ? null : movement.SupplierReturnLine.SupplierReturn.PurchaseReceiptId,
         movement.SalesFulfillmentLine != null ? movement.SalesFulfillmentLine.SalesOrderId : movement.CustomerReturnLine == null ? null : movement.CustomerReturnLine.CustomerReturn.SalesOrderId,
         movement.SalesFulfillmentLine != null ? movement.SalesFulfillmentLine.SalesFulfillment.SalesOrder.OrderNumber : movement.CustomerReturnLine == null ? null : movement.CustomerReturnLine.CustomerReturn.SalesOrder.OrderNumber,
         movement.SalesFulfillmentLine != null ? movement.SalesFulfillmentLine.SalesFulfillmentId : movement.CustomerReturnLine == null ? null : movement.CustomerReturnLine.CustomerReturn.SalesFulfillmentId,
         movement.CustomerReturnLine != null ? "CustomerReturn" : movement.SupplierReturnLine != null ? "SupplierReturn" : null,
         movement.CustomerReturnLine == null ? null : movement.CustomerReturnLine.CustomerReturnId,
         movement.CustomerReturnLine == null ? null : movement.CustomerReturnLine.CustomerReturn.ReturnNumber,
         movement.SupplierReturnLine == null ? null : movement.SupplierReturnLine.SupplierReturnId,
         movement.SupplierReturnLine == null ? null : movement.SupplierReturnLine.SupplierReturn.ReturnNumber);

    private static MovementHistoryResponse ToResponse(MovementProjection row) => new(
        row.Id, row.RequestId, row.ProductId, row.ProductSku, row.ProductName, row.ProductUnit,
        row.MovementType, row.Quantity, row.StockBefore, row.StockAfter, row.SupplierId, row.SupplierName,
         row.Reason, row.ReferenceNumber, row.Notes, row.PerformedByUserId, row.PerformedByDisplayName,
         row.OccurredAtUtc, row.PurchaseOrderId, row.PurchaseOrderNumber, row.PurchaseReceiptId,
         row.SalesOrderId, row.SalesOrderNumber, row.SalesFulfillmentId, row.ReturnKind,
         row.CustomerReturnId, row.CustomerReturnNumber, row.SupplierReturnId, row.SupplierReturnNumber);


    private sealed record MovementProjection(
        Guid Id, Guid RequestId, Guid ProductId, string ProductSku, string ProductName, string ProductUnit,
        MovementType MovementType, int Quantity, int StockBefore, int StockAfter, Guid? SupplierId,
         string? SupplierName, string? Reason, string? ReferenceNumber, string? Notes,
         Guid PerformedByUserId, string PerformedByDisplayName, DateTime OccurredAtUtc,
         Guid? PurchaseOrderId, string? PurchaseOrderNumber, Guid? PurchaseReceiptId,
         Guid? SalesOrderId, string? SalesOrderNumber, Guid? SalesFulfillmentId,
         string? ReturnKind, Guid? CustomerReturnId, string? CustomerReturnNumber,
         Guid? SupplierReturnId, string? SupplierReturnNumber);
}

internal static partial class UtcRangeParser
{
    private static readonly Regex OffsetPattern = OffsetRegex();

    public static bool TryParseOptional(string? fromText, string? toText, out UtcRange? range, out HistoryServiceError? error)
    {
        if (string.IsNullOrWhiteSpace(fromText) && string.IsNullOrWhiteSpace(toText))
        {
            range = null;
            error = null;
            return true;
        }
        if (!TryParse(fromText, out var from) || !TryParse(toText, out var to))
        {
            range = null;
            error = new("validation", "fromUtc and toUtc must be timezone-qualified ISO-8601 dates.");
            return false;
        }
        if (from >= to)
        {
            range = null;
            error = new("validation", "fromUtc must be earlier than toUtc.");
            return false;
        }
        range = new(from, to);
        error = null;
        return true;
    }

    public static bool TryParseRequired(string? fromText, string? toText, out UtcRange? range, out HistoryServiceError? error)
    {
        if (string.IsNullOrWhiteSpace(fromText) || string.IsNullOrWhiteSpace(toText))
        {
            range = null;
            error = new("validation", "fromUtc and toUtc are required.");
            return false;
        }
        return TryParseOptional(fromText, toText, out range, out error);
    }

    private static bool TryParse(string? value, out DateTimeOffset result)
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value)
            && OffsetPattern.IsMatch(value.Trim())
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result);
    }

    [GeneratedRegex("(?:Z|[+-]\\d{2}:\\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetRegex();
}
