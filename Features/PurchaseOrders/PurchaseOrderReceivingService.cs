using InventoryManagement.Api.Data;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Features.StockMovements;
using InventoryManagement.Api.Inventory;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Api.Features.PurchaseOrders;

public sealed class PurchaseOrderReceivingService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    StockPostingService stockPostingService)
{
    private const int MaxAttempts = 3;

    public async Task<(PurchaseReceiptDetailResponse? Response, PurchaseOrderServiceError? Error)> ReceiveAsync(
        Guid orderId,
        PurchaseReceiptCreateRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var normalizedReference = InventoryServiceSupport.NormalizeOptional(request.ReferenceNumber);
        var normalizedNotes = InventoryServiceSupport.NormalizeOptional(request.Notes);

        // Idempotency is checked before rowversion, state, or active-record validation.
        var completed = await FindByRequestIdAsync(request.RequestId, cancellationToken);
        if (completed is not null)
            return CompareCompleted(completed, orderId, request, normalizedReference, normalizedNotes, actorId);

        if (request.RequestId == Guid.Empty)
            return (null, new("validation", "RequestId must be a nonempty GUID."));
        if (!TryDecode(request.RowVersion, out var expectedOrderVersion))
            return (null, new("invalid_row_version", "RowVersion must be a valid Base64 value."));
        if (request.Lines is null || request.Lines.Count == 0)
            return (null, new("validation", "At least one receipt line is required."));
        if (request.Lines.Any(x => x.PurchaseOrderLineId == Guid.Empty || x.Quantity <= 0))
            return (null, new("validation", "Every receipt line requires an order line and a positive whole-number Quantity."));
        if (request.Lines.Select(x => x.PurchaseOrderLineId).Distinct().Count() != request.Lines.Count)
            return (null, new("duplicate_line", "A receipt may contain each purchase order line only once."));

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // A concurrent identical request may have committed between the initial check and this attempt.
                var raced = await db.PurchaseReceipts.AsNoTracking().Where(x => x.RequestId == request.RequestId)
                    .Select(x => x.Id).SingleOrDefaultAsync(cancellationToken);
                if (raced != Guid.Empty)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    var existing = await FindByRequestIdAsync(request.RequestId, cancellationToken);
                    return existing is null
                        ? (null, new("database_error", "The receipt could not be completed."))
                        : CompareCompleted(existing, orderId, request, normalizedReference, normalizedNotes, actorId);
                }

                var order = await db.PurchaseOrders
                    .Include(x => x.Supplier)
                    .Include(x => x.Lines).ThenInclude(x => x.Product)
                    .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
                if (order is null) return (null, new("not_found", "Purchase order was not found."));
                if (order.Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived))
                    return (null, new("invalid_transition", "Only Approved or PartiallyReceived purchase orders can receive goods."));
                if (!order.Supplier.IsActive) return (null, new("inactive_supplier", "The supplier is inactive."));

                var selectedLines = new List<(PurchaseOrderLine Line, int Quantity)>();
                foreach (var lineRequest in request.Lines)
                {
                    var line = order.Lines.SingleOrDefault(x => x.Id == lineRequest.PurchaseOrderLineId);
                    if (line is null) return (null, new("invalid_line", "Every receipt line must belong to the purchase order."));
                    if (!line.Product.IsActive) return (null, new("inactive_product", "A purchase order product is inactive."));
                    var remaining = (long)line.OrderedQuantity - line.ReceivedQuantity;
                    if (lineRequest.Quantity > remaining)
                        return (null, new("over_receipt", "A receipt line exceeds its remaining quantity."));
                    selectedLines.Add((line, lineRequest.Quantity));
                }

                db.Entry(order).Property(x => x.RowVersion).OriginalValue = expectedOrderVersion;
                var now = DateTime.UtcNow;
                var receipt = new PurchaseReceipt
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderId = order.Id,
                    RequestId = request.RequestId,
                    ReceivedByUserId = actorId,
                    ReceivedAtUtc = now,
                    ReferenceNumber = normalizedReference,
                    Notes = normalizedNotes,
                };
                db.PurchaseReceipts.Add(receipt);

                foreach (var selected in selectedLines.OrderBy(x => x.Line.ProductId))
                {
                    var posting = stockPostingService.Prepare(
                        db, selected.Line.Product, selected.Quantity, MovementType.StockIn, order.Supplier,
                        null, normalizedReference, normalizedNotes, actorId, Guid.NewGuid(), now);
                    if (!posting.Succeeded)
                        return (null, new(posting.ErrorCode!, posting.ErrorDetail!));

                    selected.Line.ReceivedQuantity = checked(selected.Line.ReceivedQuantity + selected.Quantity);
                    selected.Line.UpdatedAtUtc = now;
                    receipt.Lines.Add(new PurchaseReceiptLine
                    {
                        Id = Guid.NewGuid(),
                        PurchaseOrderId = order.Id,
                        PurchaseOrderLineId = selected.Line.Id,
                        Quantity = selected.Quantity,
                        StockMovementId = posting.Movement!.Id,
                    });
                }

                order.Status = order.Lines.All(x => x.ReceivedQuantity == x.OrderedQuantity)
                    ? PurchaseOrderStatus.Received
                    : PurchaseOrderStatus.PartiallyReceived;
                order.UpdatedAtUtc = now;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return (await GetByIdAsync(orderId, receipt.Id, cancellationToken), null);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (exception.Entries.Any(x => x.Entity is PurchaseOrder or PurchaseOrderLine))
                    return (null, new("stale_order", "The purchase order was changed by another request."));
                if (attempt == MaxAttempts)
                    return (null, new("concurrency", "Stock changed while this receipt was being processed."));
            }
            catch (DbUpdateException exception) when (InventoryServiceSupport.IsUniqueViolation(exception, "IX_PurchaseReceipts_RequestId"))
            {
                await transaction.RollbackAsync(cancellationToken);
                var existing = await FindByRequestIdAsync(request.RequestId, cancellationToken);
                return existing is null
                    ? (null, new("database_error", "The receipt could not be completed."))
                    : CompareCompleted(existing, orderId, request, normalizedReference, normalizedNotes, actorId);
            }
            catch (DbUpdateException exception) when (IsDeadlock(exception) && attempt < MaxAttempts)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }

        return (null, new("concurrency", "Stock changed while this receipt was being processed."));
    }

    public async Task<(PagedResponse<PurchaseReceiptSummaryResponse>? Response, PurchaseOrderServiceError? Error)> GetAsync(
        Guid orderId, PurchaseReceiptQuery query, CancellationToken cancellationToken)
    {
        if (!InventoryServiceSupport.IsValidPagination(query))
            return (null, new("validation", "Page must be at least 1 and pageSize must be between 1 and 100."));
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var receipts = db.PurchaseReceipts.AsNoTracking().Where(x => x.PurchaseOrderId == orderId);
        var total = await receipts.CountAsync(cancellationToken);
        var rows = await receipts.OrderByDescending(x => x.ReceivedAtUtc).ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new PurchaseReceiptSummaryResponse(
                x.Id, x.RequestId, x.ReceivedByUserId, x.ReceivedByUser.DisplayName, x.ReceivedAtUtc,
                x.ReferenceNumber, x.Notes, x.Lines.Count, x.Lines.Sum(line => (int?)line.Quantity) ?? 0))
            .ToListAsync(cancellationToken);
        return (new(rows, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)), null);
    }

    public Task<PurchaseReceiptDetailResponse?> GetByIdAsync(Guid orderId, Guid receiptId, CancellationToken cancellationToken) =>
        GetByIdAsync(orderId, receiptId, cancellationToken, contextFactory);

    private static async Task<PurchaseReceiptDetailResponse?> GetByIdAsync(
        Guid orderId, Guid receiptId, CancellationToken cancellationToken, IDbContextFactory<ApplicationDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var row = await db.PurchaseReceipts.AsNoTracking()
            .Where(x => x.Id == receiptId && x.PurchaseOrderId == orderId)
            .Select(x => new
            {
                x.Id, x.PurchaseOrderId, OrderNumber = x.PurchaseOrder.OrderNumber, OrderStatus = x.PurchaseOrder.Status,
                OrderRowVersion = x.PurchaseOrder.RowVersion, x.RequestId, x.ReceivedByUserId,
                ReceivedByDisplayName = x.ReceivedByUser.DisplayName, x.ReceivedAtUtc, x.ReferenceNumber, x.Notes,
                Lines = x.Lines.OrderBy(line => line.Id).Select(line => new
                {
                    line.Id, line.PurchaseOrderLineId, line.PurchaseOrderLine.ProductId,
                    ProductSku = line.PurchaseOrderLine.Product.Sku, ProductName = line.PurchaseOrderLine.Product.Name,
                    ProductUnit = line.PurchaseOrderLine.Product.Unit, line.Quantity, line.StockMovementId,
                    StockBefore = line.StockMovement.StockBefore, StockAfter = line.StockMovement.StockAfter,
                    line.ReturnedQuantity,
                }).ToList(),
            }).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new(
            row.Id, row.PurchaseOrderId, row.OrderNumber, row.OrderStatus, Convert.ToBase64String(row.OrderRowVersion),
            row.RequestId, row.ReceivedByUserId, row.ReceivedByDisplayName, row.ReceivedAtUtc, row.ReferenceNumber, row.Notes,
            row.Lines.Select(x => new PurchaseReceiptLineResponse(x.Id, x.PurchaseOrderLineId, x.ProductId, x.ProductSku,
                x.ProductName, x.ProductUnit, x.Quantity, x.StockMovementId, x.StockBefore, x.StockAfter, x.ReturnedQuantity, x.Quantity - x.ReturnedQuantity)).ToList());
    }

    private async Task<PurchaseReceiptDetailResponse?> FindByRequestIdAsync(Guid requestId, CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty) return null;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var id = await db.PurchaseReceipts.AsNoTracking().Where(x => x.RequestId == requestId)
            .Select(x => new { x.Id, x.PurchaseOrderId }).SingleOrDefaultAsync(cancellationToken);
        return id is null ? null : await GetByIdAsync(id.PurchaseOrderId, id.Id, cancellationToken);
    }

    private static (PurchaseReceiptDetailResponse? Response, PurchaseOrderServiceError? Error) CompareCompleted(
        PurchaseReceiptDetailResponse existing, Guid orderId, PurchaseReceiptCreateRequest request,
        string? reference, string? notes, Guid actorId)
    {
        var identical = existing.PurchaseOrderId == orderId
            && existing.ReceivedByUserId == actorId
            && string.Equals(existing.ReferenceNumber, reference, StringComparison.Ordinal)
            && string.Equals(existing.Notes, notes, StringComparison.Ordinal)
            && existing.Lines.Count == request.Lines.Count
            && existing.Lines.All(line => request.Lines.Any(x => x.PurchaseOrderLineId == line.PurchaseOrderLineId && x.Quantity == line.Quantity));
        return identical
            ? (existing, null)
            : (null, new("duplicate_request", "The requestId has already been used for a different receipt."));
    }

    private static bool TryDecode(string value, out byte[] bytes)
    {
        try { bytes = Convert.FromBase64String(value); return bytes.Length > 0; }
        catch (FormatException) { bytes = []; return false; }
    }

    private static bool IsDeadlock(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException sql && sql.Number == 1205;
}
