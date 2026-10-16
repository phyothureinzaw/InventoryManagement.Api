using System.Globalization;
using InventoryManagement.Api.Data;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Features.StockMovements;
using InventoryManagement.Api.Identity;
using InventoryManagement.Api.Inventory;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Api.Features.PurchaseOrders;

public sealed class PurchaseOrderService(ApplicationDbContext db)
{
    public async Task<(PagedResponse<PurchaseOrderSummaryResponse>? Response, PurchaseOrderServiceError? Error)> GetAsync(
        PurchaseOrderQuery query,
        CancellationToken cancellationToken)
    {
        if (!InventoryServiceSupport.IsValidPagination(query))
            return (null, new("validation", "Page must be at least 1 and pageSize must be between 1 and 100."));
        if (!TryParseStatus(query.Status, out var status))
            return (null, new("validation", "status must be Draft, Submitted, Approved, PartiallyReceived, Received, or Cancelled."));
        if (!UtcRangeParser.TryParseOptional(query.FromUtc, query.ToUtc, out var range, out var rangeError))
            return (null, new("validation", rangeError!.Detail));

        var orders = db.PurchaseOrders.AsNoTracking().AsQueryable();
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search)) orders = orders.Where(x => x.OrderNumber.Contains(search));
        if (query.SupplierId.HasValue) orders = orders.Where(x => x.SupplierId == query.SupplierId.Value);
        if (status.HasValue) orders = orders.Where(x => x.Status == status.Value);
        if (range is not null) orders = orders.Where(x => x.CreatedAtUtc >= range.FromUtc.UtcDateTime && x.CreatedAtUtc < range.ToUtc.UtcDateTime);

        var total = await orders.CountAsync(cancellationToken);
        var rows = await orders
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new
            {
                x.Id,
                x.OrderNumber,
                x.SupplierId,
                SupplierName = x.Supplier.Name,
                x.Status,
                x.CreatedByUserId,
                CreatedByDisplayName = x.CreatedByUser.DisplayName,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                LineCount = x.Lines.Count,
                OrderedQuantity = x.Lines.Sum(line => (int?)line.OrderedQuantity) ?? 0,
                ReceivedQuantity = x.Lines.Sum(line => (int?)line.ReceivedQuantity) ?? 0,
                x.RowVersion,
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(x => new PurchaseOrderSummaryResponse(
            x.Id, x.OrderNumber, x.SupplierId, x.SupplierName, x.Status, x.CreatedByUserId,
            x.CreatedByDisplayName, x.CreatedAtUtc, x.UpdatedAtUtc, x.LineCount,
            x.OrderedQuantity, x.ReceivedQuantity, Encode(x.RowVersion))).ToList();
        return (new(items, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)), null);
    }

    public async Task<PurchaseOrderDetailResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.PurchaseOrders.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.OrderNumber,
                x.SupplierId,
                SupplierName = x.Supplier.Name,
                SupplierIsActive = x.Supplier.IsActive,
                x.Status,
                x.RowVersion,
                x.UpdatedAtUtc,
                x.CreatedByUserId,
                CreatedByDisplayName = x.CreatedByUser.DisplayName,
                x.CreatedAtUtc,
                x.SubmittedByUserId,
                SubmittedByDisplayName = x.SubmittedByUser == null ? null : x.SubmittedByUser.DisplayName,
                x.SubmittedAtUtc,
                x.ApprovedByUserId,
                ApprovedByDisplayName = x.ApprovedByUser == null ? null : x.ApprovedByUser.DisplayName,
                x.ApprovedAtUtc,
                x.CancelledByUserId,
                CancelledByDisplayName = x.CancelledByUser == null ? null : x.CancelledByUser.DisplayName,
                x.CancelledAtUtc,
                x.CancellationReason,
                Lines = x.Lines.OrderBy(line => line.Id).Select(line => new
                {
                    line.Id,
                    line.ProductId,
                    ProductSku = line.Product.Sku,
                    ProductName = line.Product.Name,
                    ProductUnit = line.Product.Unit,
                    line.OrderedQuantity,
                    line.ReceivedQuantity,
                    line.RowVersion,
                    line.CreatedAtUtc,
                    line.UpdatedAtUtc,
                }).ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null) return null;
        var audit = new PurchaseOrderAuditResponse(
            row.CreatedByUserId, row.CreatedByDisplayName, row.CreatedAtUtc,
            row.SubmittedByUserId, row.SubmittedByDisplayName, row.SubmittedAtUtc,
            row.ApprovedByUserId, row.ApprovedByDisplayName, row.ApprovedAtUtc,
            row.CancelledByUserId, row.CancelledByDisplayName, row.CancelledAtUtc,
            row.CancellationReason);
        var lines = row.Lines.Select(line => new PurchaseOrderLineResponse(
            line.Id, line.ProductId, line.ProductSku, line.ProductName, line.ProductUnit,
            line.OrderedQuantity, line.ReceivedQuantity, line.OrderedQuantity - line.ReceivedQuantity,
            Encode(line.RowVersion), line.CreatedAtUtc, line.UpdatedAtUtc)).ToList();
        return new(row.Id, row.OrderNumber, row.SupplierId, row.SupplierName, row.SupplierIsActive,
            row.Status, Encode(row.RowVersion), row.UpdatedAtUtc, audit, lines);
    }

    public async Task<(PurchaseOrderDetailResponse? Response, PurchaseOrderServiceError? Error)> CreateAsync(
        PurchaseOrderCreateRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateLinesAndSupplierAsync(request.SupplierId, request.Lines, cancellationToken);
        if (validation.Error is not null) return (null, validation.Error);

        var now = DateTime.UtcNow;
        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = null!,
            SupplierId = request.SupplierId,
            Status = PurchaseOrderStatus.Draft,
            CreatedByUserId = actorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        foreach (var line in request.Lines)
        {
            order.Lines.Add(new PurchaseOrderLine
            {
                Id = Guid.NewGuid(),
                ProductId = line.ProductId,
                OrderedQuantity = line.OrderedQuantity,
                ReceivedQuantity = 0,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }
        db.PurchaseOrders.Add(order);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (InventoryServiceSupport.IsUniqueViolation(ex, "IX_PurchaseOrders_OrderNumber"))
        { return (null, new("duplicate_order_number", "The order number already exists.")); }
        return (await GetByIdAsync(order.Id, cancellationToken), null);
    }

    public async Task<(PurchaseOrderDetailResponse? Response, PurchaseOrderServiceError? Error)> UpdateAsync(
        Guid id,
        PurchaseOrderUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryDecode(request.RowVersion, out var rowVersion)) return (null, new("invalid_row_version", "RowVersion must be a valid Base64 value."));
        var order = await db.PurchaseOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (order is null) return (null, new("not_found", "Purchase order was not found."));
        if (order.Status != PurchaseOrderStatus.Draft) return (null, new("invalid_transition", "Only Draft purchase orders can be edited."));
        db.Entry(order).Property(x => x.RowVersion).OriginalValue = rowVersion;

        var validation = await ValidateLinesAndSupplierAsync(request.SupplierId, request.Lines, cancellationToken);
        if (validation.Error is not null) return (null, validation.Error);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            order.SupplierId = request.SupplierId;
            order.UpdatedAtUtc = now;
            // Save the header first so the expected order version arbitrates concurrent edits.
            await db.SaveChangesAsync(cancellationToken);

            var requestedByProduct = request.Lines.ToDictionary(x => x.ProductId);
            foreach (var existing in order.Lines.ToList())
            {
                if (!requestedByProduct.TryGetValue(existing.ProductId, out var replacement)) db.PurchaseOrderLines.Remove(existing);
                else
                {
                    existing.OrderedQuantity = replacement.OrderedQuantity;
                    existing.UpdatedAtUtc = now;
                    requestedByProduct.Remove(existing.ProductId);
                }
            }
            foreach (var line in requestedByProduct.Values)
            {
                order.Lines.Add(new PurchaseOrderLine
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderId = order.Id,
                    ProductId = line.ProductId,
                    OrderedQuantity = line.OrderedQuantity,
                    ReceivedQuantity = 0,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                });
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (null, new("concurrency", "The purchase order was changed by another request."));
        }
        return (await GetByIdAsync(order.Id, cancellationToken), null);
    }

    public Task<PurchaseOrderServiceError?> SubmitAsync(Guid id, PurchaseOrderVersionRequest request, Guid actorId, CancellationToken cancellationToken) =>
        TransitionAsync(id, request.RowVersion, actorId, PurchaseOrderStatus.Draft, PurchaseOrderStatus.Submitted, true, false, null, cancellationToken);

    public Task<PurchaseOrderServiceError?> ApproveAsync(Guid id, PurchaseOrderVersionRequest request, Guid actorId, CancellationToken cancellationToken) =>
        TransitionAsync(id, request.RowVersion, actorId, PurchaseOrderStatus.Submitted, PurchaseOrderStatus.Approved, true, false, null, cancellationToken);

    public Task<PurchaseOrderServiceError?> CancelAsync(Guid id, PurchaseOrderCancelRequest request, Guid actorId, CancellationToken cancellationToken) =>
        TransitionAsync(id, request.RowVersion, actorId, PurchaseOrderStatus.Draft, PurchaseOrderStatus.Cancelled, false, true, request.CancellationReason, cancellationToken);

    private async Task<PurchaseOrderServiceError?> TransitionAsync(
        Guid id,
        string rowVersionText,
        Guid actorId,
        PurchaseOrderStatus firstAllowedStatus,
        PurchaseOrderStatus nextStatus,
        bool requireActiveRecords,
        bool isCancellation,
        string? cancellationReason,
        CancellationToken cancellationToken)
    {
        if (!TryDecode(rowVersionText, out var rowVersion)) return new("invalid_row_version", "RowVersion must be a valid Base64 value.");
        if (isCancellation && string.IsNullOrWhiteSpace(cancellationReason)) return new("validation", "CancellationReason is required.");

        var order = await db.PurchaseOrders.Include(x => x.Lines).ThenInclude(x => x.Product).Include(x => x.Supplier).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (order is null) return new("not_found", "Purchase order was not found.");
        if (isCancellation)
        {
            if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted or PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived))
                return new("invalid_transition", "This purchase order cannot be cancelled from its current status.");
        }
        else if (order.Status != firstAllowedStatus)
        {
            return new("invalid_transition", $"A purchase order in {order.Status} status cannot transition to {nextStatus}.");
        }
        if (requireActiveRecords)
        {
            if (!order.Supplier.IsActive) return new("inactive_supplier", "The supplier is inactive.");
            if (order.Lines.Any(x => !x.Product.IsActive)) return new("inactive_product", "A purchase order product is inactive.");
        }

        db.Entry(order).Property(x => x.RowVersion).OriginalValue = rowVersion;
        var now = DateTime.UtcNow;
        order.Status = nextStatus;
        order.UpdatedAtUtc = now;
        if (nextStatus == PurchaseOrderStatus.Submitted) { order.SubmittedByUserId = actorId; order.SubmittedAtUtc = now; }
        if (nextStatus == PurchaseOrderStatus.Approved) { order.ApprovedByUserId = actorId; order.ApprovedAtUtc = now; }
        if (isCancellation) { order.CancelledByUserId = actorId; order.CancelledAtUtc = now; order.CancellationReason = cancellationReason!.Trim(); }
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new("concurrency", "The purchase order was changed by another request."); }
        return null;
    }

    private async Task<(PurchaseOrderServiceError? Error, IReadOnlyDictionary<Guid, Product> Products)> ValidateLinesAndSupplierAsync(
        Guid supplierId,
        IReadOnlyList<PurchaseOrderLineWriteRequest> lines,
        CancellationToken cancellationToken)
    {
        if (lines is null || lines.Count == 0) return (new("validation", "At least one purchase order line is required."), new Dictionary<Guid, Product>());
        if (lines.Any(x => x.ProductId == Guid.Empty || x.OrderedQuantity <= 0)) return (new("validation", "Every line requires a product and a positive whole-number OrderedQuantity."), new Dictionary<Guid, Product>());
        if (lines.Select(x => x.ProductId).Distinct().Count() != lines.Count) return (new("duplicate_product", "A product may appear only once in a purchase order."), new Dictionary<Guid, Product>());
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == supplierId, cancellationToken);
        if (supplier is null) return (new("supplier_not_found", "Supplier was not found."), new Dictionary<Guid, Product>());
        if (!supplier.IsActive) return (new("inactive_supplier", "The supplier is inactive."), new Dictionary<Guid, Product>());
        var ids = lines.Select(x => x.ProductId).ToArray();
        var products = await db.Products.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        if (products.Count != ids.Length) return (new("product_not_found", "One or more products were not found."), products);
        if (products.Values.Any(x => !x.IsActive)) return (new("inactive_product", "One or more products are inactive."), products);
        return (null, products);
    }

    private static bool TryParseStatus(string? value, out PurchaseOrderStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!Enum.TryParse<PurchaseOrderStatus>(value, true, out var parsed) || !Enum.IsDefined(parsed)) return false;
        status = parsed;
        return true;
    }

    private static bool TryDecode(string value, out byte[] bytes)
    {
        try { bytes = Convert.FromBase64String(value); return bytes.Length > 0; }
        catch (FormatException) { bytes = []; return false; }
    }

    private static string Encode(byte[] value) => Convert.ToBase64String(value);
}
