using InventoryManagement.Api.Data;
using InventoryManagement.Api.Inventory;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Api.Features.Inventory;

public sealed class ProductService(ApplicationDbContext db)
{
    public async Task<PagedResponse<ProductResponse>?> GetAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        if (!InventoryServiceSupport.IsValidPagination(query) || !new[] { "all", "low", "outOfStock" }.Contains(query.StockStatus, StringComparer.OrdinalIgnoreCase)) return null;
        var search = query.Search?.Trim();
        var products = db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) products = products.Where(x => x.Sku.Contains(search) || x.Name.Contains(search));
        if (query.CategoryId.HasValue) products = products.Where(x => x.CategoryId == query.CategoryId.Value);
        if (query.IsActive.HasValue) products = products.Where(x => x.IsActive == query.IsActive.Value);
        if (query.StockStatus.Equals("low", StringComparison.OrdinalIgnoreCase)) products = products.Where(x => x.CurrentStock <= x.LowStockThreshold);
        if (query.StockStatus.Equals("outOfStock", StringComparison.OrdinalIgnoreCase)) products = products.Where(x => x.CurrentStock == 0);
        var total = await products.CountAsync(cancellationToken);
        var projectedItems = await products.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new { x.Id, x.Sku, x.Name, x.CategoryId, CategoryName = x.Category.Name, x.Unit, x.CurrentStock, x.ReservedStock, x.LowStockThreshold, x.IsActive, x.RowVersion, x.CreatedAtUtc, x.UpdatedAtUtc })
            .ToListAsync(cancellationToken);
        var items = projectedItems.Select(x => new ProductResponse(x.Id, x.Sku, x.Name, x.CategoryId, x.CategoryName, x.Unit, x.CurrentStock, x.ReservedStock, x.CurrentStock - x.ReservedStock, x.LowStockThreshold, x.CurrentStock <= x.LowStockThreshold, x.CurrentStock == 0, x.IsActive, Convert.ToBase64String(x.RowVersion), x.CreatedAtUtc, x.UpdatedAtUtc)).ToList();
        return new(items, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public async Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Sku, x.Name, x.CategoryId, CategoryName = x.Category.Name, x.Unit, x.CurrentStock, x.ReservedStock, x.LowStockThreshold, x.IsActive, x.RowVersion, x.CreatedAtUtc, x.UpdatedAtUtc })
            .SingleOrDefaultAsync(cancellationToken);
        return product is null ? null : new(product.Id, product.Sku, product.Name, product.CategoryId, product.CategoryName, product.Unit, product.CurrentStock, product.ReservedStock, product.CurrentStock - product.ReservedStock, product.LowStockThreshold, product.CurrentStock <= product.LowStockThreshold, product.CurrentStock == 0, product.IsActive, Convert.ToBase64String(product.RowVersion), product.CreatedAtUtc, product.UpdatedAtUtc);
    }

    public async Task<(ProductResponse? Response, ServiceError? Error)> CreateAsync(ProductWriteRequest request, CancellationToken cancellationToken)
    {
        var sku = request.Sku.Trim(); var name = request.Name.Trim(); var unit = request.Unit.Trim();
        if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unit)) return (null, new("validation", "SKU, name, and unit are required."));
        var normalizedSku = InventoryNormalization.NormalizeSku(sku);
        if (await db.Products.AnyAsync(x => x.NormalizedSku == normalizedSku, cancellationToken)) return (null, new("duplicate", "A product with that SKU already exists."));
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == request.CategoryId, cancellationToken);
        if (category is null) return (null, new("category_not_found", "The category was not found."));
        if (!category.IsActive) return (null, new("inactive_category", "Products must use an active category."));
        var now = DateTime.UtcNow;
        var product = new Product { Id = Guid.NewGuid(), Sku = sku, NormalizedSku = normalizedSku, Name = name, CategoryId = category.Id, Unit = unit, CurrentStock = 0, LowStockThreshold = request.LowStockThreshold, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.Products.Add(product);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (InventoryServiceSupport.IsUniqueViolation(ex, "IX_Products_NormalizedSku")) { return (null, new("duplicate", "A product with that SKU already exists.")); }
        return (await GetByIdAsync(product.Id, cancellationToken), null);
    }

    public async Task<(ProductResponse? Response, ServiceError? Error)> UpdateAsync(Guid id, ProductWriteRequest request, CancellationToken cancellationToken)
    {
        if (!TryDecode(request.RowVersion ?? string.Empty, out var rowVersion)) return (null, new("validation", "RowVersion must be a valid Base64 value."));
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null) return (null, new("not_found", "Product was not found."));
        db.Entry(product).Property(x => x.RowVersion).OriginalValue = rowVersion;
        var sku = request.Sku.Trim(); var name = request.Name.Trim(); var unit = request.Unit.Trim();
        if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unit)) return (null, new("validation", "SKU, name, and unit are required."));
        var normalizedSku = InventoryNormalization.NormalizeSku(sku);
        if (await db.Products.AnyAsync(x => x.Id != id && x.NormalizedSku == normalizedSku, cancellationToken)) return (null, new("duplicate", "A product with that SKU already exists."));
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == request.CategoryId, cancellationToken);
        if (category is null) return (null, new("category_not_found", "The category was not found."));
        if (!category.IsActive && product.CategoryId != category.Id) return (null, new("inactive_category", "Products must use an active category."));
        product.Sku = sku; product.NormalizedSku = normalizedSku; product.Name = name; product.Unit = unit; product.LowStockThreshold = request.LowStockThreshold; product.CategoryId = category.Id; product.UpdatedAtUtc = DateTime.UtcNow;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return (null, new("concurrency", "The product was changed by another request.")); }
        catch (DbUpdateException ex) when (InventoryServiceSupport.IsUniqueViolation(ex, "IX_Products_NormalizedSku")) { return (null, new("duplicate", "A product with that SKU already exists.")); }
        return (await GetByIdAsync(id, cancellationToken), null);
    }

    public async Task<ServiceError?> SetStatusAsync(Guid id, ProductStatusRequest request, CancellationToken cancellationToken)
    {
        if (!request.IsActive.HasValue) return new("validation", "isActive is required.");
        if (!TryDecode(request.RowVersion, out var rowVersion)) return new("validation", "RowVersion must be a valid Base64 value.");
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null) return new("not_found", "Product was not found.");
        db.Entry(product).Property(x => x.RowVersion).OriginalValue = rowVersion;
        if (request.IsActive.Value && !await db.Categories.AnyAsync(x => x.Id == product.CategoryId && x.IsActive, cancellationToken)) return new("inactive_category", "A product cannot be reactivated while its category is inactive.");
        product.IsActive = request.IsActive.Value; product.UpdatedAtUtc = DateTime.UtcNow;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new("concurrency", "The product was changed by another request."); }
        return null;
    }

    private static bool TryDecode(string value, out byte[] bytes)
    {
        try { bytes = Convert.FromBase64String(value); return bytes.Length > 0; }
        catch (FormatException) { bytes = []; return false; }
    }
}
