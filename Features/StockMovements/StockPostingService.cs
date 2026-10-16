using InventoryManagement.Api.Data;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Inventory;

namespace InventoryManagement.Api.Features.StockMovements;

// Prepares a stock mutation on the caller's context. The caller owns the transaction and save.
public sealed class StockPostingService
{
    public StockPostingResult Prepare(
        ApplicationDbContext db,
        Product product,
        int quantity,
        MovementType movementType,
        Supplier? supplier,
        string? reason,
        string? referenceNumber,
        string? notes,
        Guid performedByUserId,
        Guid requestId,
        DateTime occurredAtUtc)
    {
        if (requestId == Guid.Empty) return Failure("validation", "RequestId must be a nonempty GUID.");
        if (quantity <= 0) return Failure("validation", "Quantity must be a positive integer.");
        if (!product.IsActive) return Failure("inactive_product", "The product is inactive.");
        if (supplier is not null && !supplier.IsActive) return Failure("inactive_supplier", "The supplier is inactive.");

        var normalizedReason = InventoryServiceSupport.NormalizeOptional(reason);
        var normalizedReference = InventoryServiceSupport.NormalizeOptional(referenceNumber);
        var normalizedNotes = InventoryServiceSupport.NormalizeOptional(notes);
        if (movementType == MovementType.StockOut && string.IsNullOrWhiteSpace(normalizedReason))
            return Failure("validation", "Reason is required for stock-out.");

        var stockBefore = product.CurrentStock;
        var stockAfterLong = movementType == MovementType.StockIn
            ? (long)stockBefore + quantity
            : (long)stockBefore - quantity;
        if (movementType == MovementType.StockOut)
        {
            var availableStock = (long)product.CurrentStock - product.ReservedStock;
            if (quantity > availableStock)
                return Failure("insufficient_available_stock", "There is not enough available stock for this operation.");
        }
        if (stockAfterLong > int.MaxValue)
            return Failure("stock_overflow", "The resulting stock exceeds the supported limit.");

        var movement = new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            MovementType = movementType,
            Quantity = quantity,
            StockBefore = stockBefore,
            StockAfter = (int)stockAfterLong,
            SupplierId = supplier?.Id,
            Reason = normalizedReason,
            ReferenceNumber = normalizedReference,
            Notes = normalizedNotes,
            PerformedByUserId = performedByUserId,
            OccurredAtUtc = occurredAtUtc,
            RequestId = requestId,
        };
        product.CurrentStock = movement.StockAfter;
        product.UpdatedAtUtc = occurredAtUtc;
        db.StockMovements.Add(movement);
        return new(movement, null, null);
    }

    // Consumes an existing reservation; unlike standalone stock-out this path may use inactive historical products.
    public StockPostingResult PrepareReservedStockOut(
        ApplicationDbContext db,
        Product product,
        int quantity,
        string? reason,
        string? referenceNumber,
        string? notes,
        Guid performedByUserId,
        Guid requestId,
        DateTime occurredAtUtc)
    {
        if (requestId == Guid.Empty) return Failure("validation", "RequestId must be a nonempty GUID.");
        if (quantity <= 0) return Failure("validation", "Quantity must be a positive integer.");
        var normalizedReason = InventoryServiceSupport.NormalizeOptional(reason);
        if (string.IsNullOrWhiteSpace(normalizedReason)) return Failure("validation", "Reason is required for stock-out.");
        if (quantity > product.ReservedStock) return Failure("insufficient_reserved_stock", "There is not enough reserved stock for this operation.");
        if (quantity > product.CurrentStock) return Failure("insufficient_stock", "There is not enough physical stock for this operation.");

        var normalizedReference = InventoryServiceSupport.NormalizeOptional(referenceNumber);
        var normalizedNotes = InventoryServiceSupport.NormalizeOptional(notes);
        var movement = new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            MovementType = MovementType.StockOut,
            Quantity = quantity,
            StockBefore = product.CurrentStock,
            StockAfter = product.CurrentStock - quantity,
            Reason = normalizedReason,
            ReferenceNumber = normalizedReference,
            Notes = normalizedNotes,
            PerformedByUserId = performedByUserId,
            OccurredAtUtc = occurredAtUtc,
            RequestId = requestId,
        };
        product.CurrentStock -= quantity;
        product.ReservedStock -= quantity;
        product.UpdatedAtUtc = occurredAtUtc;
        db.StockMovements.Add(movement);
        return new(movement, null, null);
    }

    // Return posting deliberately has its own paths so inactive historical source records do not
    // weaken the validation used by standalone stock operations.
    public StockPostingResult PrepareCustomerReturnStockIn(
        ApplicationDbContext db,
        Product product,
        int quantity,
        string? reason,
        string? referenceNumber,
        string? notes,
        Guid performedByUserId,
        Guid requestId,
        DateTime occurredAtUtc)
    {
        if (requestId == Guid.Empty) return Failure("validation", "RequestId must be a nonempty GUID.");
        if (quantity <= 0) return Failure("validation", "Quantity must be a positive integer.");
        var stockAfter = (long)product.CurrentStock + quantity;
        if (stockAfter > int.MaxValue) return Failure("stock_overflow", "The resulting stock exceeds the supported limit.");
        var movement = new StockMovement
        {
            Id = Guid.NewGuid(), ProductId = product.Id, MovementType = MovementType.StockIn,
            Quantity = quantity, StockBefore = product.CurrentStock, StockAfter = (int)stockAfter,
            Reason = InventoryServiceSupport.NormalizeOptional(reason),
            ReferenceNumber = InventoryServiceSupport.NormalizeOptional(referenceNumber),
            Notes = InventoryServiceSupport.NormalizeOptional(notes), PerformedByUserId = performedByUserId,
            OccurredAtUtc = occurredAtUtc, RequestId = requestId,
        };
        product.CurrentStock = movement.StockAfter;
        product.UpdatedAtUtc = occurredAtUtc;
        db.StockMovements.Add(movement);
        return new(movement, null, null);
    }

    public StockPostingResult PrepareSupplierReturnStockOut(
        ApplicationDbContext db,
        Product product,
        int quantity,
        string reason,
        string? referenceNumber,
        string? notes,
        Guid performedByUserId,
        Guid requestId,
        DateTime occurredAtUtc)
    {
        if (requestId == Guid.Empty) return Failure("validation", "RequestId must be a nonempty GUID.");
        if (quantity <= 0) return Failure("validation", "Quantity must be a positive integer.");
        var normalizedReason = InventoryServiceSupport.NormalizeOptional(reason);
        if (string.IsNullOrWhiteSpace(normalizedReason)) return Failure("validation", "Reason is required for supplier return.");
        var availableStock = (long)product.CurrentStock - product.ReservedStock;
        if (quantity > availableStock) return Failure("insufficient_available_stock", "There is not enough available stock for this supplier return.");
        var movement = new StockMovement
        {
            Id = Guid.NewGuid(), ProductId = product.Id, MovementType = MovementType.StockOut,
            Quantity = quantity, StockBefore = product.CurrentStock, StockAfter = product.CurrentStock - quantity,
            SupplierId = null, Reason = normalizedReason,
            ReferenceNumber = InventoryServiceSupport.NormalizeOptional(referenceNumber),
            Notes = InventoryServiceSupport.NormalizeOptional(notes), PerformedByUserId = performedByUserId,
            OccurredAtUtc = occurredAtUtc, RequestId = requestId,
        };
        product.CurrentStock = movement.StockAfter;
        product.UpdatedAtUtc = occurredAtUtc;
        db.StockMovements.Add(movement);
        return new(movement, null, null);
    }

    private static StockPostingResult Failure(string code, string detail) => new(null, code, detail);
}

public sealed record StockPostingResult(StockMovement? Movement, string? ErrorCode, string? ErrorDetail)
{
    public bool Succeeded => Movement is not null;
}
