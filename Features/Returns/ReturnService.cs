using InventoryManagement.Api.Data;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Features.StockMovements;
using InventoryManagement.Api.Inventory;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Api.Features.Returns;

public sealed class ReturnService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    StockPostingService stockPostingService)
{
    private const int MaxAttempts = 3;

    public async Task<(CustomerReturnDetailResponse? Response, ReturnServiceError? Error)> CreateCustomerAsync(
        CustomerReturnCreateRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        var reason = InventoryServiceSupport.NormalizeOptional(request.Reason);
        var reference = InventoryServiceSupport.NormalizeOptional(request.ReferenceNumber);
        var notes = InventoryServiceSupport.NormalizeOptional(request.Notes);
        var lines = request.Lines ?? [];
        var completed = await FindCustomerByRequestIdAsync(request.RequestId, cancellationToken);
        if (completed is not null) return CompareCustomer(completed, request, reason, reference, notes, actorId);
        var validation = ValidateRequest(request.RequestId, reason, lines.Select(x => (x.SalesFulfillmentLineId, x.Quantity)));
        if (validation is not null) return (null, validation);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var raced = await db.CustomerReturns.AsNoTracking().Where(x => x.RequestId == request.RequestId).Select(x => x.Id).SingleOrDefaultAsync(cancellationToken);
                if (raced != Guid.Empty)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    var existing = await FindCustomerByRequestIdAsync(request.RequestId, cancellationToken);
                    return existing is null ? (null, new("database_error", "The customer return could not be completed.")) : CompareCustomer(existing, request, reason, reference, notes, actorId);
                }

                var fulfillment = await db.SalesFulfillments
                    .Include(x => x.SalesOrder).ThenInclude(x => x.Customer)
                    .Include(x => x.Lines).ThenInclude(x => x.SalesOrderLine).ThenInclude(x => x.Product)
                    .SingleOrDefaultAsync(x => x.Id == request.SalesFulfillmentId, cancellationToken);
                if (fulfillment is null) return (null, new("not_found", "The sales fulfillment was not found."));
                var selected = new List<(SalesFulfillmentLine Line, Product Product, int Quantity)>();
                foreach (var requested in lines)
                {
                    var line = fulfillment.Lines.SingleOrDefault(x => x.Id == requested.SalesFulfillmentLineId);
                    if (line is null) return (null, new("invalid_line", "Every customer return line must belong to the selected sales fulfillment."));
                    var remaining = (long)line.Quantity - line.ReturnedQuantity;
                    if (requested.Quantity > remaining) return (null, new("over_return", "A customer return line exceeds its returnable quantity."));
                    selected.Add((line, line.SalesOrderLine.Product, requested.Quantity));
                }

                var now = DateTime.UtcNow;
                var customerReturn = new CustomerReturn
                {
                    Id = Guid.NewGuid(), RequestId = request.RequestId, SalesOrderId = fulfillment.SalesOrderId,
                    SalesFulfillmentId = fulfillment.Id, CustomerId = fulfillment.SalesOrder.CustomerId,
                    ReturnedByUserId = actorId, ReturnedAtUtc = now, Reason = reason!, ReferenceNumber = reference, Notes = notes,
                };
                db.CustomerReturns.Add(customerReturn);
                foreach (var item in selected.OrderBy(x => x.Product.Id).ThenBy(x => x.Line.Id))
                {
                    var posting = stockPostingService.PrepareCustomerReturnStockIn(
                        db, item.Product, item.Quantity, reason, reference, notes, actorId, Guid.NewGuid(), now);
                    if (!posting.Succeeded) return (null, new(posting.ErrorCode!, posting.ErrorDetail!));
                    item.Line.ReturnedQuantity = checked(item.Line.ReturnedQuantity + item.Quantity);
                    customerReturn.Lines.Add(new CustomerReturnLine
                    {
                        Id = Guid.NewGuid(), SalesOrderId = fulfillment.SalesOrderId, SalesFulfillmentId = fulfillment.Id,
                        SalesFulfillmentLineId = item.Line.Id, Quantity = item.Quantity, StockMovementId = posting.Movement!.Id,
                    });
                }
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return (await GetCustomerByIdAsync(customerReturn.Id, cancellationToken), null);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (attempt == MaxAttempts) return (null, new("concurrency", "Source or stock data changed while this return was being processed."));
            }
            catch (DbUpdateException exception) when (InventoryServiceSupport.IsUniqueViolation(exception, "IX_CustomerReturns_RequestId"))
            {
                await transaction.RollbackAsync(cancellationToken);
                var existing = await FindCustomerByRequestIdAsync(request.RequestId, cancellationToken);
                return existing is null ? (null, new("database_error", "The customer return could not be completed.")) : CompareCustomer(existing, request, reason, reference, notes, actorId);
            }
            catch (DbUpdateException exception) when (IsDeadlock(exception) && attempt < MaxAttempts)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }
        return (null, new("concurrency", "Source or stock data changed while this return was being processed."));
    }

    public async Task<(SupplierReturnDetailResponse? Response, ReturnServiceError? Error)> CreateSupplierAsync(
        SupplierReturnCreateRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        var reason = InventoryServiceSupport.NormalizeOptional(request.Reason);
        var reference = InventoryServiceSupport.NormalizeOptional(request.ReferenceNumber);
        var notes = InventoryServiceSupport.NormalizeOptional(request.Notes);
        var lines = request.Lines ?? [];
        var completed = await FindSupplierByRequestIdAsync(request.RequestId, cancellationToken);
        if (completed is not null) return CompareSupplier(completed, request, reason, reference, notes, actorId);
        var validation = ValidateRequest(request.RequestId, reason, lines.Select(x => (x.PurchaseReceiptLineId, x.Quantity)));
        if (validation is not null) return (null, validation);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var raced = await db.SupplierReturns.AsNoTracking().Where(x => x.RequestId == request.RequestId).Select(x => x.Id).SingleOrDefaultAsync(cancellationToken);
                if (raced != Guid.Empty)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    var existing = await FindSupplierByRequestIdAsync(request.RequestId, cancellationToken);
                    return existing is null ? (null, new("database_error", "The supplier return could not be completed.")) : CompareSupplier(existing, request, reason, reference, notes, actorId);
                }

                var receipt = await db.PurchaseReceipts
                    .Include(x => x.PurchaseOrder).ThenInclude(x => x.Supplier)
                    .Include(x => x.Lines).ThenInclude(x => x.PurchaseOrderLine).ThenInclude(x => x.Product)
                    .SingleOrDefaultAsync(x => x.Id == request.PurchaseReceiptId, cancellationToken);
                if (receipt is null) return (null, new("not_found", "The purchase receipt was not found."));
                var selected = new List<(PurchaseReceiptLine Line, Product Product, int Quantity)>();
                foreach (var requested in lines)
                {
                    var line = receipt.Lines.SingleOrDefault(x => x.Id == requested.PurchaseReceiptLineId);
                    if (line is null) return (null, new("invalid_line", "Every supplier return line must belong to the selected purchase receipt."));
                    var remaining = (long)line.Quantity - line.ReturnedQuantity;
                    if (requested.Quantity > remaining) return (null, new("over_return", "A supplier return line exceeds its returnable quantity."));
                    selected.Add((line, line.PurchaseOrderLine.Product, requested.Quantity));
                }

                var now = DateTime.UtcNow;
                var supplierReturn = new SupplierReturn
                {
                    Id = Guid.NewGuid(), RequestId = request.RequestId, PurchaseOrderId = receipt.PurchaseOrderId,
                    PurchaseReceiptId = receipt.Id, SupplierId = receipt.PurchaseOrder.SupplierId,
                    ReturnedByUserId = actorId, ReturnedAtUtc = now, Reason = reason!, ReferenceNumber = reference, Notes = notes,
                };
                db.SupplierReturns.Add(supplierReturn);
                foreach (var item in selected.OrderBy(x => x.Product.Id).ThenBy(x => x.Line.Id))
                {
                    var posting = stockPostingService.PrepareSupplierReturnStockOut(
                        db, item.Product, item.Quantity, reason!, reference, notes, actorId, Guid.NewGuid(), now);
                    if (!posting.Succeeded) return (null, new(posting.ErrorCode!, posting.ErrorDetail!));
                    item.Line.ReturnedQuantity = checked(item.Line.ReturnedQuantity + item.Quantity);
                    supplierReturn.Lines.Add(new SupplierReturnLine
                    {
                        Id = Guid.NewGuid(), PurchaseOrderId = receipt.PurchaseOrderId, PurchaseReceiptId = receipt.Id,
                        PurchaseReceiptLineId = item.Line.Id, Quantity = item.Quantity, StockMovementId = posting.Movement!.Id,
                    });
                }
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return (await GetSupplierByIdAsync(supplierReturn.Id, cancellationToken), null);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (attempt == MaxAttempts) return (null, new("concurrency", "Source or stock data changed while this return was being processed."));
            }
            catch (DbUpdateException exception) when (InventoryServiceSupport.IsUniqueViolation(exception, "IX_SupplierReturns_RequestId"))
            {
                await transaction.RollbackAsync(cancellationToken);
                var existing = await FindSupplierByRequestIdAsync(request.RequestId, cancellationToken);
                return existing is null ? (null, new("database_error", "The supplier return could not be completed.")) : CompareSupplier(existing, request, reason, reference, notes, actorId);
            }
            catch (DbUpdateException exception) when (IsDeadlock(exception) && attempt < MaxAttempts)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }
        return (null, new("concurrency", "Source or stock data changed while this return was being processed."));
    }

    public async Task<(PagedResponse<CustomerReturnSummaryResponse>? Response, ReturnServiceError? Error)> GetCustomersAsync(CustomerReturnQuery query, CancellationToken cancellationToken)
    {
        if (!InventoryServiceSupport.IsValidPagination(query)) return (null, new("validation", "Page must be at least 1 and pageSize must be between 1 and 100."));
        if (!UtcRangeParser.TryParseOptional(query.FromUtc, query.ToUtc, out var range, out var rangeError)) return (null, new("validation", rangeError!.Detail));
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var returns = db.CustomerReturns.AsNoTracking();
        if (query.SalesOrderId.HasValue) returns = returns.Where(x => x.SalesOrderId == query.SalesOrderId.Value);
        if (query.SalesFulfillmentId.HasValue) returns = returns.Where(x => x.SalesFulfillmentId == query.SalesFulfillmentId.Value);
        if (query.CustomerId.HasValue) returns = returns.Where(x => x.CustomerId == query.CustomerId.Value);
        if (!string.IsNullOrWhiteSpace(query.CustomerSearch)) returns = returns.Where(x => x.Customer.Name.Contains(query.CustomerSearch.Trim()));
        if (range is not null) returns = returns.Where(x => x.ReturnedAtUtc >= range.FromUtc.UtcDateTime && x.ReturnedAtUtc < range.ToUtc.UtcDateTime);
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search)) returns = returns.Where(x => x.ReturnNumber.Contains(search) || (x.ReferenceNumber != null && x.ReferenceNumber.Contains(search)));
        var total = await returns.CountAsync(cancellationToken);
        var rows = await returns.OrderByDescending(x => x.ReturnedAtUtc).ThenByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CustomerReturnSummaryResponse(x.Id, x.ReturnNumber, x.RequestId, x.SalesOrderId, x.SalesOrder.OrderNumber, x.SalesFulfillmentId, x.CustomerId, x.Customer.Name, x.ReturnedByUserId, x.ReturnedByUser.DisplayName, x.ReturnedAtUtc, x.Reason, x.ReferenceNumber, x.Notes, x.Lines.Count, x.Lines.Sum(line => (int?)line.Quantity) ?? 0)).ToListAsync(cancellationToken);
        return (new(rows, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)), null);
    }

    public async Task<(PagedResponse<CustomerReturnSourceResponse>? Response, ReturnServiceError? Error)> GetCustomerSourcesAsync(CustomerReturnSourceQuery query, CancellationToken cancellationToken)
    {
        if (!InventoryServiceSupport.IsValidPagination(query)) return (null, new("validation", "Page must be at least 1 and pageSize must be between 1 and 100."));
        if (!UtcRangeParser.TryParseOptional(query.FromUtc, query.ToUtc, out var range, out var rangeError)) return (null, new("validation", rangeError!.Detail));
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var sources = db.SalesFulfillments.AsNoTracking();
        if (query.SalesFulfillmentId.HasValue) sources = sources.Where(x => x.Id == query.SalesFulfillmentId.Value);
        if (query.SalesOrderId.HasValue) sources = sources.Where(x => x.SalesOrderId == query.SalesOrderId.Value);
        if (query.CustomerId.HasValue) sources = sources.Where(x => x.SalesOrder.CustomerId == query.CustomerId.Value);
        if (range is not null) sources = sources.Where(x => x.FulfilledAtUtc >= range.FromUtc.UtcDateTime && x.FulfilledAtUtc < range.ToUtc.UtcDateTime);
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search)) sources = sources.Where(x => x.SalesOrder.OrderNumber.Contains(search) || x.SalesOrder.Customer.Name.Contains(search) || (x.ReferenceNumber != null && x.ReferenceNumber.Contains(search)));
        var total = await sources.CountAsync(cancellationToken);
        var rows = await sources.OrderByDescending(x => x.FulfilledAtUtc).ThenByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CustomerReturnSourceResponse(x.Id, x.SalesOrderId, x.SalesOrder.OrderNumber, x.SalesOrder.Status, x.SalesOrder.CustomerId, x.SalesOrder.Customer.Name, x.SalesOrder.Customer.IsActive, x.FulfilledByUserId, x.FulfilledByUser.DisplayName, x.FulfilledAtUtc, x.ReferenceNumber,
                x.Lines.OrderBy(line => line.Id).Select(line => new CustomerReturnSourceLineResponse(line.Id, line.SalesOrderLine.ProductId, line.SalesOrderLine.Product.Sku, line.SalesOrderLine.Product.Name, line.SalesOrderLine.Product.Unit, line.Quantity, line.ReturnedQuantity, line.Quantity - line.ReturnedQuantity, line.SalesOrderLine.Product.CurrentStock, line.SalesOrderLine.Product.ReservedStock, line.SalesOrderLine.Product.CurrentStock - line.SalesOrderLine.Product.ReservedStock)).ToList())).ToListAsync(cancellationToken);
        return (new(rows, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)), null);
    }

    public async Task<(PagedResponse<SupplierReturnSummaryResponse>? Response, ReturnServiceError? Error)> GetSuppliersAsync(SupplierReturnQuery query, CancellationToken cancellationToken)
    {
        if (!InventoryServiceSupport.IsValidPagination(query)) return (null, new("validation", "Page must be at least 1 and pageSize must be between 1 and 100."));
        if (!UtcRangeParser.TryParseOptional(query.FromUtc, query.ToUtc, out var range, out var rangeError)) return (null, new("validation", rangeError!.Detail));
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var returns = db.SupplierReturns.AsNoTracking();
        if (query.PurchaseOrderId.HasValue) returns = returns.Where(x => x.PurchaseOrderId == query.PurchaseOrderId.Value);
        if (query.PurchaseReceiptId.HasValue) returns = returns.Where(x => x.PurchaseReceiptId == query.PurchaseReceiptId.Value);
        if (query.SupplierId.HasValue) returns = returns.Where(x => x.SupplierId == query.SupplierId.Value);
        if (!string.IsNullOrWhiteSpace(query.SupplierSearch)) returns = returns.Where(x => x.Supplier.Name.Contains(query.SupplierSearch.Trim()));
        if (range is not null) returns = returns.Where(x => x.ReturnedAtUtc >= range.FromUtc.UtcDateTime && x.ReturnedAtUtc < range.ToUtc.UtcDateTime);
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search)) returns = returns.Where(x => x.ReturnNumber.Contains(search) || (x.ReferenceNumber != null && x.ReferenceNumber.Contains(search)));
        var total = await returns.CountAsync(cancellationToken);
        var rows = await returns.OrderByDescending(x => x.ReturnedAtUtc).ThenByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new SupplierReturnSummaryResponse(x.Id, x.ReturnNumber, x.RequestId, x.PurchaseOrderId, x.PurchaseOrder.OrderNumber, x.PurchaseReceiptId, x.SupplierId, x.Supplier.Name, x.ReturnedByUserId, x.ReturnedByUser.DisplayName, x.ReturnedAtUtc, x.Reason, x.ReferenceNumber, x.Notes, x.Lines.Count, x.Lines.Sum(line => (int?)line.Quantity) ?? 0)).ToListAsync(cancellationToken);
        return (new(rows, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)), null);
    }

    public async Task<(PagedResponse<SupplierReturnSourceResponse>? Response, ReturnServiceError? Error)> GetSupplierSourcesAsync(SupplierReturnSourceQuery query, CancellationToken cancellationToken)
    {
        if (!InventoryServiceSupport.IsValidPagination(query)) return (null, new("validation", "Page must be at least 1 and pageSize must be between 1 and 100."));
        if (!UtcRangeParser.TryParseOptional(query.FromUtc, query.ToUtc, out var range, out var rangeError)) return (null, new("validation", rangeError!.Detail));
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var sources = db.PurchaseReceipts.AsNoTracking();
        if (query.PurchaseReceiptId.HasValue) sources = sources.Where(x => x.Id == query.PurchaseReceiptId.Value);
        if (query.PurchaseOrderId.HasValue) sources = sources.Where(x => x.PurchaseOrderId == query.PurchaseOrderId.Value);
        if (query.SupplierId.HasValue) sources = sources.Where(x => x.PurchaseOrder.SupplierId == query.SupplierId.Value);
        if (range is not null) sources = sources.Where(x => x.ReceivedAtUtc >= range.FromUtc.UtcDateTime && x.ReceivedAtUtc < range.ToUtc.UtcDateTime);
        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search)) sources = sources.Where(x => x.PurchaseOrder.OrderNumber.Contains(search) || x.PurchaseOrder.Supplier.Name.Contains(search) || (x.ReferenceNumber != null && x.ReferenceNumber.Contains(search)));
        var total = await sources.CountAsync(cancellationToken);
        var rows = await sources.OrderByDescending(x => x.ReceivedAtUtc).ThenByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new SupplierReturnSourceResponse(x.Id, x.PurchaseOrderId, x.PurchaseOrder.OrderNumber, x.PurchaseOrder.Status, x.PurchaseOrder.SupplierId, x.PurchaseOrder.Supplier.Name, x.PurchaseOrder.Supplier.IsActive, x.ReceivedByUserId, x.ReceivedByUser.DisplayName, x.ReceivedAtUtc, x.ReferenceNumber,
                x.Lines.OrderBy(line => line.Id).Select(line => new SupplierReturnSourceLineResponse(line.Id, line.PurchaseOrderLine.ProductId, line.PurchaseOrderLine.Product.Sku, line.PurchaseOrderLine.Product.Name, line.PurchaseOrderLine.Product.Unit, line.Quantity, line.ReturnedQuantity, line.Quantity - line.ReturnedQuantity, line.PurchaseOrderLine.Product.CurrentStock, line.PurchaseOrderLine.Product.ReservedStock, line.PurchaseOrderLine.Product.CurrentStock - line.PurchaseOrderLine.Product.ReservedStock)).ToList())).ToListAsync(cancellationToken);
        return (new(rows, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)), null);
    }

    public async Task<CustomerReturnDetailResponse?> GetCustomerByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.CustomerReturns.AsNoTracking().Where(x => x.Id == id).Select(x => new
        {
            x.Id, x.ReturnNumber, x.RequestId, x.SalesOrderId, SalesOrderNumber = x.SalesOrder.OrderNumber, SalesOrderStatus = x.SalesOrder.Status,
            x.SalesFulfillmentId, x.CustomerId, CustomerName = x.Customer.Name, CustomerIsActive = x.Customer.IsActive,
            x.ReturnedByUserId, ReturnedByDisplayName = x.ReturnedByUser.DisplayName, x.ReturnedAtUtc, x.Reason, x.ReferenceNumber, x.Notes,
            Lines = x.Lines.OrderBy(line => line.Id).Select(line => new { line.Id, line.SalesFulfillmentLineId, ProductId = line.SalesFulfillmentLine.SalesOrderLine.ProductId, ProductSku = line.SalesFulfillmentLine.SalesOrderLine.Product.Sku, ProductName = line.SalesFulfillmentLine.SalesOrderLine.Product.Name, ProductUnit = line.SalesFulfillmentLine.SalesOrderLine.Product.Unit, line.Quantity, line.StockMovementId, StockBefore = line.StockMovement.StockBefore, StockAfter = line.StockMovement.StockAfter, CurrentStock = line.SalesFulfillmentLine.SalesOrderLine.Product.CurrentStock, ReservedStock = line.SalesFulfillmentLine.SalesOrderLine.Product.ReservedStock }).ToList(),
        }).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new(row.Id, row.ReturnNumber, row.RequestId, row.SalesOrderId, row.SalesOrderNumber, row.SalesOrderStatus, row.SalesFulfillmentId, row.CustomerId, row.CustomerName, row.CustomerIsActive, row.ReturnedByUserId, row.ReturnedByDisplayName, row.ReturnedAtUtc, row.Reason, row.ReferenceNumber, row.Notes, row.Lines.Select(x => new CustomerReturnLineResponse(x.Id, x.SalesFulfillmentLineId, x.ProductId, x.ProductSku, x.ProductName, x.ProductUnit, x.Quantity, x.StockMovementId, x.StockBefore, x.StockAfter, x.CurrentStock, x.ReservedStock, x.CurrentStock - x.ReservedStock)).ToList());
    }

    public async Task<SupplierReturnDetailResponse?> GetSupplierByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.SupplierReturns.AsNoTracking().Where(x => x.Id == id).Select(x => new
        {
            x.Id, x.ReturnNumber, x.RequestId, x.PurchaseOrderId, PurchaseOrderNumber = x.PurchaseOrder.OrderNumber, PurchaseOrderStatus = x.PurchaseOrder.Status,
            x.PurchaseReceiptId, x.SupplierId, SupplierName = x.Supplier.Name, SupplierIsActive = x.Supplier.IsActive,
            x.ReturnedByUserId, ReturnedByDisplayName = x.ReturnedByUser.DisplayName, x.ReturnedAtUtc, x.Reason, x.ReferenceNumber, x.Notes,
            Lines = x.Lines.OrderBy(line => line.Id).Select(line => new { line.Id, line.PurchaseReceiptLineId, ProductId = line.PurchaseReceiptLine.PurchaseOrderLine.ProductId, ProductSku = line.PurchaseReceiptLine.PurchaseOrderLine.Product.Sku, ProductName = line.PurchaseReceiptLine.PurchaseOrderLine.Product.Name, ProductUnit = line.PurchaseReceiptLine.PurchaseOrderLine.Product.Unit, line.Quantity, line.StockMovementId, StockBefore = line.StockMovement.StockBefore, StockAfter = line.StockMovement.StockAfter, CurrentStock = line.PurchaseReceiptLine.PurchaseOrderLine.Product.CurrentStock, ReservedStock = line.PurchaseReceiptLine.PurchaseOrderLine.Product.ReservedStock }).ToList(),
        }).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new(row.Id, row.ReturnNumber, row.RequestId, row.PurchaseOrderId, row.PurchaseOrderNumber, row.PurchaseOrderStatus, row.PurchaseReceiptId, row.SupplierId, row.SupplierName, row.SupplierIsActive, row.ReturnedByUserId, row.ReturnedByDisplayName, row.ReturnedAtUtc, row.Reason, row.ReferenceNumber, row.Notes, row.Lines.Select(x => new SupplierReturnLineResponse(x.Id, x.PurchaseReceiptLineId, x.ProductId, x.ProductSku, x.ProductName, x.ProductUnit, x.Quantity, x.StockMovementId, x.StockBefore, x.StockAfter, x.CurrentStock, x.ReservedStock, x.CurrentStock - x.ReservedStock)).ToList());
    }

    private async Task<CustomerReturnDetailResponse?> FindCustomerByRequestIdAsync(Guid requestId, CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty) return null;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var id = await db.CustomerReturns.AsNoTracking().Where(x => x.RequestId == requestId).Select(x => x.Id).SingleOrDefaultAsync(cancellationToken);
        return id == Guid.Empty ? null : await GetCustomerByIdAsync(id, cancellationToken);
    }

    private async Task<SupplierReturnDetailResponse?> FindSupplierByRequestIdAsync(Guid requestId, CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty) return null;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var id = await db.SupplierReturns.AsNoTracking().Where(x => x.RequestId == requestId).Select(x => x.Id).SingleOrDefaultAsync(cancellationToken);
        return id == Guid.Empty ? null : await GetSupplierByIdAsync(id, cancellationToken);
    }

    private static ReturnServiceError? ValidateRequest(Guid requestId, string? reason, IEnumerable<(Guid Id, int Quantity)>? lines)
    {
        if (requestId == Guid.Empty) return new("validation", "RequestId must be a nonempty GUID.");
        if (string.IsNullOrWhiteSpace(reason)) return new("validation", "Reason must not be blank.");
        if (lines is null || !lines.Any()) return new("validation", "At least one return line is required.");
        var materialized = lines.ToList();
        if (materialized.Any(x => x.Id == Guid.Empty || x.Quantity <= 0)) return new("validation", "Every return line requires a source line and a positive whole-number Quantity.");
        if (materialized.Select(x => x.Id).Distinct().Count() != materialized.Count) return new("duplicate_line", "A return may contain each source line only once.");
        return null;
    }

    private static (CustomerReturnDetailResponse? Response, ReturnServiceError? Error) CompareCustomer(CustomerReturnDetailResponse existing, CustomerReturnCreateRequest request, string? reason, string? reference, string? notes, Guid actorId)
    {
        var identical = existing.SalesFulfillmentId == request.SalesFulfillmentId && existing.ReturnedByUserId == actorId && string.Equals(existing.Reason, reason, StringComparison.Ordinal) && string.Equals(existing.ReferenceNumber, reference, StringComparison.Ordinal) && string.Equals(existing.Notes, notes, StringComparison.Ordinal) && SameLines(existing.Lines.Select(x => (x.SalesFulfillmentLineId, x.Quantity)), (request.Lines ?? []).Select(x => (x.SalesFulfillmentLineId, x.Quantity)));
        return identical ? (existing, null) : (null, new("duplicate_request", "The requestId has already been used for a different customer return."));
    }

    private static (SupplierReturnDetailResponse? Response, ReturnServiceError? Error) CompareSupplier(SupplierReturnDetailResponse existing, SupplierReturnCreateRequest request, string? reason, string? reference, string? notes, Guid actorId)
    {
        var identical = existing.PurchaseReceiptId == request.PurchaseReceiptId && existing.ReturnedByUserId == actorId && string.Equals(existing.Reason, reason, StringComparison.Ordinal) && string.Equals(existing.ReferenceNumber, reference, StringComparison.Ordinal) && string.Equals(existing.Notes, notes, StringComparison.Ordinal) && SameLines(existing.Lines.Select(x => (x.PurchaseReceiptLineId, x.Quantity)), (request.Lines ?? []).Select(x => (x.PurchaseReceiptLineId, x.Quantity)));
        return identical ? (existing, null) : (null, new("duplicate_request", "The requestId has already been used for a different supplier return."));
    }

    private static bool SameLines(IEnumerable<(Guid Id, int Quantity)> left, IEnumerable<(Guid Id, int Quantity)> right) => left.OrderBy(x => x.Id).SequenceEqual(right.OrderBy(x => x.Id));
    private static bool IsDeadlock(DbUpdateException exception) => exception.GetBaseException() is SqlException sql && sql.Number == 1205;
}
