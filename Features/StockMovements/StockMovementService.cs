using InventoryManagement.Api.Data;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Inventory;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Api.Features.StockMovements;

public sealed class StockMovementService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    StockPostingService postingService)
{
    private const int MaxAttempts = 3;

    public Task<(StockMovementResponse? Response, StockMovementServiceError? Error)> StockInAsync(
        StockInRequest request,
        Guid performedByUserId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            request.RequestId,
            request.ProductId,
            request.Quantity,
            MovementType.StockIn,
            request.SupplierId,
            null,
            request.ReferenceNumber,
            request.Notes,
            performedByUserId,
            cancellationToken);

    public Task<(StockMovementResponse? Response, StockMovementServiceError? Error)> StockOutAsync(
        StockOutRequest request,
        Guid performedByUserId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            request.RequestId,
            request.ProductId,
            request.Quantity,
            MovementType.StockOut,
            null,
            request.Reason,
            request.ReferenceNumber,
            request.Notes,
            performedByUserId,
            cancellationToken);

    private async Task<(StockMovementResponse? Response, StockMovementServiceError? Error)> ExecuteAsync(
        Guid requestId,
        Guid productId,
        int quantity,
        MovementType movementType,
        Guid? supplierId,
        string? reason,
        string? referenceNumber,
        string? notes,
        Guid performedByUserId,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty) return (null, new("validation", "RequestId must be a nonempty GUID."));
        if (quantity <= 0) return (null, new("validation", "Quantity must be a positive integer."));

        var normalizedReason = Normalize(reason);
        var normalizedReference = Normalize(referenceNumber);
        var normalizedNotes = Normalize(notes);
        if (movementType == MovementType.StockOut && string.IsNullOrWhiteSpace(normalizedReason))
            return (null, new("validation", "Reason is required for stock-out."));

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await db.StockMovements.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RequestId == requestId, cancellationToken);
            if (existing is not null)
                return CompareExisting(existing, requestId, productId, quantity, movementType, supplierId, normalizedReason, normalizedReference, normalizedNotes, performedByUserId);

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId, cancellationToken);
                if (product is null) return (null, new("not_found", "Product was not found."));
                Supplier? supplier = null;
                if (supplierId.HasValue)
                {
                    supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == supplierId.Value, cancellationToken);
                    if (supplier is null) return (null, new("supplier_not_found", "Supplier was not found."));
                }

                var occurredAtUtc = DateTime.UtcNow;
                var posting = postingService.Prepare(db, product, quantity, movementType, supplier,
                    normalizedReason, normalizedReference, normalizedNotes, performedByUserId, requestId, occurredAtUtc);
                if (!posting.Succeeded) return (null, new(posting.ErrorCode!, posting.ErrorDetail!));
                var movement = posting.Movement!;

                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return (ToResponse(movement), null);
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    if (attempt == MaxAttempts)
                        return (null, new("concurrency", "The stock changed while this operation was being processed."));
                }
                catch (DbUpdateException exception)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    if (InventoryServiceSupport.IsUniqueViolation(exception, "IX_StockMovements_RequestId"))
                        return await ResolveDuplicateAsync(requestId, productId, quantity, movementType, supplierId, normalizedReason, normalizedReference, normalizedNotes, performedByUserId, cancellationToken);
                    throw;
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (attempt == MaxAttempts)
                    return (null, new("concurrency", "The stock changed while this operation was being processed."));
            }
        }

        return (null, new("concurrency", "The stock changed while this operation was being processed."));
    }

    private async Task<(StockMovementResponse? Response, StockMovementServiceError? Error)> ResolveDuplicateAsync(
        Guid requestId,
        Guid productId,
        int quantity,
        MovementType movementType,
        Guid? supplierId,
        string? reason,
        string? referenceNumber,
        string? notes,
        Guid performedByUserId,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.StockMovements.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RequestId == requestId, cancellationToken);
        return existing is null
            ? (null, new("database_error", "The stock operation could not be completed."))
            : CompareExisting(existing, requestId, productId, quantity, movementType, supplierId, reason, referenceNumber, notes, performedByUserId);
    }

    private static (StockMovementResponse? Response, StockMovementServiceError? Error) CompareExisting(
        StockMovement existing,
        Guid requestId,
        Guid productId,
        int quantity,
        MovementType movementType,
        Guid? supplierId,
        string? reason,
        string? referenceNumber,
        string? notes,
        Guid performedByUserId)
    {
        var isIdentical = existing.ProductId == productId
            && existing.Quantity == quantity
            && existing.MovementType == movementType
            && existing.SupplierId == supplierId
            && string.Equals(existing.Reason, reason, StringComparison.Ordinal)
            && string.Equals(existing.ReferenceNumber, referenceNumber, StringComparison.Ordinal)
            && string.Equals(existing.Notes, notes, StringComparison.Ordinal)
            && existing.PerformedByUserId == performedByUserId;
        return isIdentical
            ? (ToResponse(existing), null)
            : (null, new("duplicate_request", "The requestId has already been used for a different operation."));
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static StockMovementResponse ToResponse(StockMovement movement) => new(
        movement.Id,
        movement.ProductId,
        movement.MovementType,
        movement.Quantity,
        movement.StockBefore,
        movement.StockAfter,
        movement.SupplierId,
        movement.Reason,
        movement.ReferenceNumber,
        movement.Notes,
        movement.PerformedByUserId,
        movement.OccurredAtUtc,
        movement.RequestId);
}
