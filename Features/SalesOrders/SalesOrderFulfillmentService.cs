using InventoryManagement.Api.Data;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Features.StockMovements;
using InventoryManagement.Api.Inventory;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Api.Features.SalesOrders;

public sealed class SalesOrderFulfillmentService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    StockPostingService stockPostingService)
{
    private const int MaxAttempts = 3;

    public async Task<(SalesFulfillmentDetailResponse? Response, SalesOrderServiceError? Error)> FulfillAsync(
        Guid orderId,
        SalesFulfillmentCreateRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var normalizedReference = InventoryServiceSupport.NormalizeOptional(request.ReferenceNumber);
        var normalizedNotes = InventoryServiceSupport.NormalizeOptional(request.Notes);
        var completed = await FindByRequestIdAsync(request.RequestId, cancellationToken);
        if (completed is not null)
            return CompareCompleted(completed, orderId, request, normalizedReference, normalizedNotes, actorId);

        if (request.RequestId == Guid.Empty) return (null, new("validation", "RequestId must be a nonempty GUID."));
        if (!TryDecode(request.RowVersion, out var expectedOrderVersion)) return (null, new("invalid_row_version", "RowVersion must be a valid Base64 value."));
        if (request.Lines is null || request.Lines.Count == 0) return (null, new("validation", "At least one fulfillment line is required."));
        if (request.Lines.Any(x => x.SalesOrderLineId == Guid.Empty || x.Quantity <= 0)) return (null, new("validation", "Every fulfillment line requires an order line and a positive whole-number Quantity."));
        if (request.Lines.Select(x => x.SalesOrderLineId).Distinct().Count() != request.Lines.Count) return (null, new("duplicate_line", "A fulfillment may contain each sales order line only once."));

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var raced = await db.SalesFulfillments.AsNoTracking().Where(x => x.RequestId == request.RequestId)
                    .Select(x => x.Id).SingleOrDefaultAsync(cancellationToken);
                if (raced != Guid.Empty)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    var existing = await FindByRequestIdAsync(request.RequestId, cancellationToken);
                    return existing is null
                        ? (null, new("database_error", "The fulfillment could not be completed."))
                        : CompareCompleted(existing, orderId, request, normalizedReference, normalizedNotes, actorId);
                }

                var order = await db.SalesOrders
                    .Include(x => x.Lines).ThenInclude(x => x.Reservation).ThenInclude(x => x.Product)
                    .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
                if (order is null) return (null, new("not_found", "Sales order was not found."));
                if (!order.RowVersion.SequenceEqual(expectedOrderVersion)) return (null, new("stale_order", "The sales order was changed by another request; reload it before fulfilling."));
                if (order.Status is not (SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyFulfilled))
                    return (null, new("invalid_transition", "Only Confirmed or PartiallyFulfilled sales orders can be fulfilled."));

                var selected = new List<(SalesOrderLine Line, SalesOrderReservation Reservation, int Quantity)>();
                foreach (var requested in request.Lines)
                {
                    var line = order.Lines.SingleOrDefault(x => x.Id == requested.SalesOrderLineId);
                    if (line is null || line.Reservation is null)
                        return (null, new("invalid_line", "Every fulfillment line must belong to the sales order and have a reservation."));
                    var activeReservation = line.Reservation.ReservedQuantity - line.Reservation.FulfilledQuantity - line.Reservation.ReleasedQuantity;
                    if (requested.Quantity > activeReservation)
                        return (null, new("over_fulfillment", "A fulfillment line exceeds its active reservation."));
                    selected.Add((line, line.Reservation, requested.Quantity));
                }

                var now = DateTime.UtcNow;
                var fulfillment = new SalesFulfillment
                {
                    Id = Guid.NewGuid(),
                    SalesOrderId = order.Id,
                    RequestId = request.RequestId,
                    FulfilledByUserId = actorId,
                    FulfilledAtUtc = now,
                    ReferenceNumber = normalizedReference,
                    Notes = normalizedNotes,
                };
                db.SalesFulfillments.Add(fulfillment);

                foreach (var item in selected.OrderBy(x => x.Line.ProductId))
                {
                    var movementRequestId = Guid.NewGuid();
                    var posting = stockPostingService.PrepareReservedStockOut(
                        db, item.Line.Product, item.Quantity, "Sales order fulfillment", normalizedReference,
                        normalizedNotes, actorId, movementRequestId, now);
                    if (!posting.Succeeded)
                        return (null, new(posting.ErrorCode!, posting.ErrorDetail!));

                    item.Line.FulfilledQuantity = checked(item.Line.FulfilledQuantity + item.Quantity);
                    item.Line.UpdatedAtUtc = now;
                    item.Reservation.FulfilledQuantity = checked(item.Reservation.FulfilledQuantity + item.Quantity);
                    item.Reservation.UpdatedAtUtc = now;
                    db.SalesFulfillmentLines.Add(new SalesFulfillmentLine
                    {
                        Id = Guid.NewGuid(),
                        SalesFulfillmentId = fulfillment.Id,
                        SalesOrderId = order.Id,
                        SalesOrderLineId = item.Line.Id,
                        Quantity = item.Quantity,
                        StockMovementId = posting.Movement!.Id,
                    });
                }

                order.Status = order.Lines.All(x => x.FulfilledQuantity == x.OrderedQuantity)
                    ? SalesOrderStatus.Fulfilled
                    : SalesOrderStatus.PartiallyFulfilled;
                order.UpdatedAtUtc = now;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return (await GetByIdAsync(order.Id, fulfillment.Id, cancellationToken), null);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (exception.Entries.Any(x => x.Entity is SalesOrder))
                    return (null, new("stale_order", "The sales order was changed by another request; reload it before fulfilling."));
                if (attempt == MaxAttempts)
                    return (null, new("concurrency", "Stock or reservation data changed while this fulfillment was being processed."));
            }
            catch (DbUpdateException exception) when (InventoryServiceSupport.IsUniqueViolation(exception, "IX_SalesFulfillments_RequestId"))
            {
                await transaction.RollbackAsync(cancellationToken);
                var existing = await FindByRequestIdAsync(request.RequestId, cancellationToken);
                return existing is null
                    ? (null, new("database_error", "The fulfillment could not be completed."))
                    : CompareCompleted(existing, orderId, request, normalizedReference, normalizedNotes, actorId);
            }
            catch (DbUpdateException exception) when (IsDeadlock(exception) && attempt < MaxAttempts)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }

        return (null, new("concurrency", "Stock or reservation data changed while this fulfillment was being processed."));
    }

    public async Task<(PagedResponse<SalesFulfillmentSummaryResponse>? Response, SalesOrderServiceError? Error)> GetAsync(
        Guid orderId, SalesFulfillmentQuery query, CancellationToken cancellationToken)
    {
        if (!InventoryServiceSupport.IsValidPagination(query)) return (null, new("validation", "Page must be at least 1 and pageSize must be between 1 and 100."));
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.SalesOrders.AnyAsync(x => x.Id == orderId, cancellationToken)) return (null, new("not_found", "Sales order was not found."));
        var fulfillments = db.SalesFulfillments.AsNoTracking().Where(x => x.SalesOrderId == orderId);
        var total = await fulfillments.CountAsync(cancellationToken);
        var rows = await fulfillments.OrderByDescending(x => x.FulfilledAtUtc).ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new SalesFulfillmentSummaryResponse(x.Id, x.RequestId, x.FulfilledByUserId, x.FulfilledByUser.DisplayName, x.FulfilledAtUtc, x.ReferenceNumber, x.Notes, x.Lines.Count, x.Lines.Sum(line => (int?)line.Quantity) ?? 0))
            .ToListAsync(cancellationToken);
        return (new(rows, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)), null);
    }

    public async Task<SalesFulfillmentDetailResponse?> GetByIdAsync(Guid orderId, Guid fulfillmentId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.SalesFulfillments.AsNoTracking()
            .Where(x => x.Id == fulfillmentId && x.SalesOrderId == orderId)
            .Select(x => new
            {
                x.Id, x.SalesOrderId, OrderNumber = x.SalesOrder.OrderNumber, OrderStatus = x.SalesOrder.Status,
                OrderRowVersion = x.SalesOrder.RowVersion, x.RequestId, x.FulfilledByUserId,
                FulfilledByDisplayName = x.FulfilledByUser.DisplayName, x.FulfilledAtUtc, x.ReferenceNumber, x.Notes,
                Lines = x.Lines.OrderBy(line => line.Id).Select(line => new
                {
                    line.Id, line.SalesOrderLineId, line.SalesOrderLine.ProductId,
                    ProductSku = line.SalesOrderLine.Product.Sku, ProductName = line.SalesOrderLine.Product.Name,
                     ProductUnit = line.SalesOrderLine.Product.Unit, line.Quantity, line.StockMovementId,
                     StockBefore = line.StockMovement.StockBefore, StockAfter = line.StockMovement.StockAfter,
                     line.ReturnedQuantity,
                    CurrentStock = line.SalesOrderLine.Product.CurrentStock,
                    ReservedStock = line.SalesOrderLine.Product.ReservedStock,
                }).ToList(),
            }).SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        return new(row.Id, row.SalesOrderId, row.OrderNumber, row.OrderStatus, Encode(row.OrderRowVersion), row.RequestId,
            row.FulfilledByUserId, row.FulfilledByDisplayName, row.FulfilledAtUtc, row.ReferenceNumber, row.Notes,
            row.Lines.Select(x => new SalesFulfillmentLineResponse(x.Id, x.SalesOrderLineId, x.ProductId, x.ProductSku,
                x.ProductName, x.ProductUnit, x.Quantity, x.StockMovementId, x.StockBefore, x.StockAfter,
                x.CurrentStock, x.ReservedStock, x.CurrentStock - x.ReservedStock, x.ReturnedQuantity, x.Quantity - x.ReturnedQuantity)).ToList());
    }

    public async Task<(SalesOrderDetailResponse? Response, SalesOrderServiceError? Error)> CancelAsync(
        Guid orderId, SalesOrderCancelRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        if (!TryDecode(request.RowVersion, out var expectedOrderVersion)) return (null, new("invalid_row_version", "RowVersion must be a valid Base64 value."));
        var reason = InventoryServiceSupport.NormalizeOptional(request.CancellationReason);
        if (string.IsNullOrWhiteSpace(reason)) return (null, new("validation", "CancellationReason must not be blank."));

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await db.SalesOrders.Include(x => x.Lines).ThenInclude(x => x.Reservation).ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null) return (null, new("not_found", "Sales order was not found."));
        if (!order.RowVersion.SequenceEqual(expectedOrderVersion)) return (null, new("stale_order", "The sales order was changed by another request; reload it before cancelling."));
        if (order.Status is not (SalesOrderStatus.Draft or SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyFulfilled))
            return (null, new("invalid_transition", "Only Draft, Confirmed, or PartiallyFulfilled sales orders can be cancelled."));

        var now = DateTime.UtcNow;
        foreach (var line in order.Lines)
        {
            if (line.Reservation is null) continue;
            var active = line.Reservation.ReservedQuantity - line.Reservation.FulfilledQuantity - line.Reservation.ReleasedQuantity;
            if (active <= 0) continue;
            line.Reservation.ReleasedQuantity = checked(line.Reservation.ReleasedQuantity + active);
            line.Reservation.UpdatedAtUtc = now;
            line.Reservation.Product.ReservedStock -= active;
            line.Reservation.Product.UpdatedAtUtc = now;
        }
        order.Status = SalesOrderStatus.Cancelled;
        order.CancelledByUserId = actorId;
        order.CancelledAtUtc = now;
        order.CancellationReason = reason;
        order.UpdatedAtUtc = now;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (null, new("stale_order", "The sales order was changed by another request; reload it before cancelling."));
        }
        return (await GetOrderByIdAsync(orderId, cancellationToken), null);
    }

    private async Task<SalesFulfillmentDetailResponse?> FindByRequestIdAsync(Guid requestId, CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty) return null;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var id = await db.SalesFulfillments.AsNoTracking().Where(x => x.RequestId == requestId)
            .Select(x => new { x.Id, x.SalesOrderId }).SingleOrDefaultAsync(cancellationToken);
        return id is null ? null : await GetByIdAsync(id.SalesOrderId, id.Id, cancellationToken);
    }

    private static (SalesFulfillmentDetailResponse? Response, SalesOrderServiceError? Error) CompareCompleted(
        SalesFulfillmentDetailResponse existing,
        Guid orderId,
        SalesFulfillmentCreateRequest request,
        string? referenceNumber,
        string? notes,
        Guid actorId)
    {
        var identical = existing.SalesOrderId == orderId
            && existing.FulfilledByUserId == actorId
            && string.Equals(existing.ReferenceNumber, referenceNumber, StringComparison.Ordinal)
            && string.Equals(existing.Notes, notes, StringComparison.Ordinal)
            && existing.Lines.Count == request.Lines.Count
            && existing.Lines.All(line => request.Lines.Any(x => x.SalesOrderLineId == line.SalesOrderLineId && x.Quantity == line.Quantity));
        return identical
            ? (existing, null)
            : (null, new("duplicate_request", "The requestId has already been used for a different fulfillment."));
    }

    private async Task<SalesOrderDetailResponse?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.SalesOrders.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.OrderNumber, x.CustomerId, CustomerName = x.Customer.Name, CustomerIsActive = x.Customer.IsActive,
                x.Status, x.RowVersion, x.UpdatedAtUtc, x.CreatedByUserId, CreatedByDisplayName = x.CreatedByUser.DisplayName, x.CreatedAtUtc,
                x.ConfirmedByUserId, ConfirmedByDisplayName = x.ConfirmedByUser == null ? null : x.ConfirmedByUser.DisplayName, x.ConfirmedAtUtc,
                x.CancelledByUserId, CancelledByDisplayName = x.CancelledByUser == null ? null : x.CancelledByUser.DisplayName, x.CancelledAtUtc, x.CancellationReason,
                Lines = x.Lines.OrderBy(line => line.Id).Select(line => new
                {
                    line.Id, line.ProductId, ProductSku = line.Product.Sku, ProductName = line.Product.Name, ProductUnit = line.Product.Unit,
                    ProductIsActive = line.Product.IsActive, line.OrderedQuantity, line.FulfilledQuantity,
                    ActiveReservationQuantity = line.Reservation == null ? 0 : line.Reservation.ReservedQuantity - line.Reservation.FulfilledQuantity - line.Reservation.ReleasedQuantity,
                    line.Product.CurrentStock, line.Product.ReservedStock, line.Product.RowVersion, line.CreatedAtUtc, line.UpdatedAtUtc,
                }).ToList(),
            }).SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        var audit = new SalesOrderAuditResponse(row.CreatedByUserId, row.CreatedByDisplayName, row.CreatedAtUtc, row.ConfirmedByUserId,
            row.ConfirmedByDisplayName, row.ConfirmedAtUtc, row.CancelledByUserId, row.CancelledByDisplayName, row.CancelledAtUtc, row.CancellationReason);
        var lines = row.Lines.Select(line => new SalesOrderLineResponse(line.Id, line.ProductId, line.ProductSku, line.ProductName, line.ProductUnit,
            line.ProductIsActive, line.OrderedQuantity, line.FulfilledQuantity, line.OrderedQuantity - line.FulfilledQuantity, line.ActiveReservationQuantity,
            line.CurrentStock, line.ReservedStock, line.CurrentStock - line.ReservedStock, Encode(line.RowVersion), line.CreatedAtUtc, line.UpdatedAtUtc)).ToList();
        return new(row.Id, row.OrderNumber, row.CustomerId, row.CustomerName, row.CustomerIsActive, row.Status, Encode(row.RowVersion), row.UpdatedAtUtc, audit, lines);
    }

    private static bool TryDecode(string value, out byte[] bytes)
    {
        try { bytes = Convert.FromBase64String(value); return bytes.Length > 0; }
        catch (FormatException) { bytes = []; return false; }
    }

    private static bool IsDeadlock(DbUpdateException exception) => exception.GetBaseException() is SqlException sql && sql.Number == 1205;
    private static string Encode(byte[] value) => Convert.ToBase64String(value);
}
