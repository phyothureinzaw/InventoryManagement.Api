using InventoryManagement.Api.Data;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Features.StockMovements;
using InventoryManagement.Api.Inventory;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Api.Features.SalesOrders;

public sealed class SalesOrderService(ApplicationDbContext db)
{
    public async Task<(PagedResponse<SalesOrderSummaryResponse>? Response, SalesOrderServiceError? Error)> GetAsync(SalesOrderQuery query, CancellationToken cancellationToken)
    {
        if (!InventoryServiceSupport.IsValidPagination(query)) return (null, new("validation", "Page must be at least 1 and pageSize must be between 1 and 100."));
        if (!TryParseStatus(query.Status, out var status)) return (null, new("validation", "status must be Draft, Confirmed, PartiallyFulfilled, Fulfilled, or Cancelled."));
        if (!UtcRangeParser.TryParseOptional(query.FromUtc, query.ToUtc, out var range, out var rangeError)) return (null, new("validation", rangeError!.Detail));
        var orders = db.SalesOrders.AsNoTracking().AsQueryable();
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search)) orders = orders.Where(x => x.OrderNumber.Contains(search));
        if (query.CustomerId.HasValue) orders = orders.Where(x => x.CustomerId == query.CustomerId.Value);
        if (status.HasValue) orders = orders.Where(x => x.Status == status.Value);
        if (range is not null) orders = orders.Where(x => x.CreatedAtUtc >= range.FromUtc.UtcDateTime && x.CreatedAtUtc < range.ToUtc.UtcDateTime);
        var total = await orders.CountAsync(cancellationToken);
        var rows = await orders.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new SalesOrderSummaryResponse(x.Id, x.OrderNumber, x.CustomerId, x.Customer.Name, x.Status, x.CreatedByUserId, x.CreatedByUser.DisplayName, x.CreatedAtUtc, x.UpdatedAtUtc, x.Lines.Count, x.Lines.Sum(line => (int?)line.OrderedQuantity) ?? 0, x.Lines.Sum(line => (int?)line.FulfilledQuantity) ?? 0, Encode(x.RowVersion)))
            .ToListAsync(cancellationToken);
        return (new(rows, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)), null);
    }

    public async Task<SalesOrderDetailResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.SalesOrders.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.OrderNumber, x.CustomerId, CustomerName = x.Customer.Name, CustomerIsActive = x.Customer.IsActive,
                x.Status, x.RowVersion, x.UpdatedAtUtc,
                x.CreatedByUserId, CreatedByDisplayName = x.CreatedByUser.DisplayName, x.CreatedAtUtc,
                x.ConfirmedByUserId, ConfirmedByDisplayName = x.ConfirmedByUser == null ? null : x.ConfirmedByUser.DisplayName, x.ConfirmedAtUtc,
                x.CancelledByUserId, CancelledByDisplayName = x.CancelledByUser == null ? null : x.CancelledByUser.DisplayName, x.CancelledAtUtc, x.CancellationReason,
                Lines = x.Lines.OrderBy(line => line.Id).Select(line => new
                {
                    line.Id, line.ProductId, ProductSku = line.Product.Sku, ProductName = line.Product.Name, ProductUnit = line.Product.Unit, ProductIsActive = line.Product.IsActive,
                    line.OrderedQuantity, line.FulfilledQuantity, ActiveReservationQuantity = line.Reservation == null ? 0 : line.Reservation.ReservedQuantity - line.Reservation.FulfilledQuantity - line.Reservation.ReleasedQuantity,
                    line.Product.CurrentStock, line.Product.ReservedStock, line.Product.RowVersion, line.CreatedAtUtc, line.UpdatedAtUtc,
                }).ToList(),
            }).SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        var audit = new SalesOrderAuditResponse(row.CreatedByUserId, row.CreatedByDisplayName, row.CreatedAtUtc, row.ConfirmedByUserId, row.ConfirmedByDisplayName, row.ConfirmedAtUtc, row.CancelledByUserId, row.CancelledByDisplayName, row.CancelledAtUtc, row.CancellationReason);
        var lines = row.Lines.Select(line => new SalesOrderLineResponse(line.Id, line.ProductId, line.ProductSku, line.ProductName, line.ProductUnit, line.ProductIsActive, line.OrderedQuantity, line.FulfilledQuantity, line.OrderedQuantity - line.FulfilledQuantity, line.ActiveReservationQuantity, line.CurrentStock, line.ReservedStock, line.CurrentStock - line.ReservedStock, Encode(line.RowVersion), line.CreatedAtUtc, line.UpdatedAtUtc)).ToList();
        return new(row.Id, row.OrderNumber, row.CustomerId, row.CustomerName, row.CustomerIsActive, row.Status, Encode(row.RowVersion), row.UpdatedAtUtc, audit, lines);
    }

    public async Task<(SalesOrderDetailResponse? Response, SalesOrderServiceError? Error)> CreateAsync(SalesOrderCreateRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        var validation = await ValidateCustomerAndLinesAsync(request.CustomerId, request.Lines, cancellationToken);
        if (validation.Error is not null) return (null, validation.Error);
        var now = DateTime.UtcNow;
        var order = new SalesOrder { Id = Guid.NewGuid(), OrderNumber = null!, CustomerId = request.CustomerId, Status = SalesOrderStatus.Draft, CreatedByUserId = actorId, CreatedAtUtc = now, UpdatedAtUtc = now };
        foreach (var line in request.Lines) order.Lines.Add(new SalesOrderLine { Id = Guid.NewGuid(), ProductId = line.ProductId, OrderedQuantity = line.OrderedQuantity, CreatedAtUtc = now, UpdatedAtUtc = now });
        db.SalesOrders.Add(order);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (InventoryServiceSupport.IsUniqueViolation(ex, "IX_SalesOrders_OrderNumber")) { return (null, new("duplicate_order_number", "The order number already exists.")); }
        return (await GetByIdAsync(order.Id, cancellationToken), null);
    }

    public async Task<(SalesOrderDetailResponse? Response, SalesOrderServiceError? Error)> UpdateAsync(Guid id, SalesOrderUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!TryDecode(request.RowVersion, out var rowVersion)) return (null, new("invalid_row_version", "RowVersion must be a valid Base64 value."));
        var order = await db.SalesOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (order is null) return (null, new("not_found", "Sales order was not found."));
        if (order.Status != SalesOrderStatus.Draft) return (null, new("invalid_transition", "Only Draft sales orders can be edited."));
        db.Entry(order).Property(x => x.RowVersion).OriginalValue = rowVersion;
        var validation = await ValidateCustomerAndLinesAsync(request.CustomerId, request.Lines, cancellationToken);
        if (validation.Error is not null) return (null, validation.Error);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            order.CustomerId = request.CustomerId;
            order.UpdatedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
            var requestedByProduct = request.Lines.ToDictionary(x => x.ProductId);
            foreach (var existing in order.Lines.ToList())
            {
                if (!requestedByProduct.TryGetValue(existing.ProductId, out var replacement)) db.SalesOrderLines.Remove(existing);
                else { existing.OrderedQuantity = replacement.OrderedQuantity; existing.UpdatedAtUtc = now; requestedByProduct.Remove(existing.ProductId); }
            }
            foreach (var line in requestedByProduct.Values) order.Lines.Add(new SalesOrderLine { Id = Guid.NewGuid(), SalesOrderId = order.Id, ProductId = line.ProductId, OrderedQuantity = line.OrderedQuantity, CreatedAtUtc = now, UpdatedAtUtc = now });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(cancellationToken); return (null, new("concurrency", "The sales order was changed by another request.")); }
        return (await GetByIdAsync(order.Id, cancellationToken), null);
    }

    public async Task<(SalesOrderDetailResponse? Response, SalesOrderServiceError? Error)> ConfirmAsync(Guid id, SalesOrderConfirmRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        if (!TryDecode(request.RowVersion, out var rowVersion)) return (null, new("invalid_row_version", "RowVersion must be a valid Base64 value."));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await db.SalesOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (order is null) return (null, new("not_found", "Sales order was not found."));
        if (order.Status != SalesOrderStatus.Draft) return (null, new("invalid_transition", "Only Draft sales orders can be confirmed."));
        db.Entry(order).Property(x => x.RowVersion).OriginalValue = rowVersion;

        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == order.CustomerId, cancellationToken);
        if (customer is null) return (null, new("customer_not_found", "Customer was not found."));
        if (!customer.IsActive) return (null, new("inactive_customer", "The customer is inactive."));

        var lines = order.Lines.OrderBy(x => x.ProductId).ToList();
        var productIds = lines.Select(x => x.ProductId).ToArray();
        var products = await db.Products.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        if (products.Count != productIds.Length) return (null, new("product_not_found", "One or more products were not found."));
        if (products.Values.Any(x => !x.IsActive)) return (null, new("inactive_product", "One or more products are inactive."));
        if (lines.Any(line => (long)line.OrderedQuantity > (long)products[line.ProductId].CurrentStock - products[line.ProductId].ReservedStock))
            return (null, new("insufficient_available_stock", "There is not enough available stock to confirm the sales order."));

        var now = DateTime.UtcNow;
        foreach (var line in lines)
        {
            var product = products[line.ProductId];
            product.ReservedStock += line.OrderedQuantity;
            product.UpdatedAtUtc = now;
            db.SalesOrderReservations.Add(new SalesOrderReservation
            {
                Id = Guid.NewGuid(),
                SalesOrderId = order.Id,
                SalesOrderLineId = line.Id,
                ProductId = product.Id,
                ReservedQuantity = line.OrderedQuantity,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        order.Status = SalesOrderStatus.Confirmed;
        order.ConfirmedByUserId = actorId;
        order.ConfirmedAtUtc = now;
        order.UpdatedAtUtc = now;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (null, new("concurrency", "The sales order or product stock was changed by another request."));
        }
        return (await GetByIdAsync(order.Id, cancellationToken), null);
    }

    private async Task<(SalesOrderServiceError? Error, IReadOnlyDictionary<Guid, Product> Products)> ValidateCustomerAndLinesAsync(Guid customerId, IReadOnlyList<SalesOrderLineWriteRequest> lines, CancellationToken cancellationToken)
    {
        if (lines is null || lines.Count == 0) return (new("validation", "At least one sales order line is required."), new Dictionary<Guid, Product>());
        if (lines.Any(x => x.ProductId == Guid.Empty || x.OrderedQuantity <= 0)) return (new("validation", "Every line requires a product and a positive whole-number OrderedQuantity."), new Dictionary<Guid, Product>());
        if (lines.Select(x => x.ProductId).Distinct().Count() != lines.Count) return (new("duplicate_product", "A product may appear only once in a sales order."), new Dictionary<Guid, Product>());
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        if (customer is null) return (new("customer_not_found", "Customer was not found."), new Dictionary<Guid, Product>());
        if (!customer.IsActive) return (new("inactive_customer", "The customer is inactive."), new Dictionary<Guid, Product>());
        var ids = lines.Select(x => x.ProductId).ToArray();
        var products = await db.Products.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        if (products.Count != ids.Length) return (new("product_not_found", "One or more products were not found."), products);
        if (products.Values.Any(x => !x.IsActive)) return (new("inactive_product", "One or more products are inactive."), products);
        return (null, products);
    }

    private static bool TryParseStatus(string? value, out SalesOrderStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!Enum.TryParse<SalesOrderStatus>(value, true, out var parsed) || !Enum.IsDefined(parsed)) return false;
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
