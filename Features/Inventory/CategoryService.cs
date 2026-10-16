using InventoryManagement.Api.Data;
using InventoryManagement.Api.Inventory;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Api.Features.Inventory;

public sealed class CategoryService(ApplicationDbContext db)
{
    public async Task<PagedResponse<CategoryResponse>?> GetAsync(CategoryQuery query, CancellationToken cancellationToken)
    {
        if (!InventoryServiceSupport.IsValidPagination(query)) return null;
        var search = query.Search?.Trim();
        var categories = db.Categories.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) categories = categories.Where(x => x.Name.Contains(search));
        if (query.IsActive.HasValue) categories = categories.Where(x => x.IsActive == query.IsActive.Value);
        var total = await categories.CountAsync(cancellationToken);
        var items = await categories.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CategoryResponse(x.Id, x.Name, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        return new(items, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public async Task<CategoryResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Categories.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new CategoryResponse(x.Id, x.Name, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<(CategoryResponse? Response, ServiceError? Error)> CreateAsync(CategoryWriteRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return (null, new("validation", "Name is required."));
        var normalized = InventoryNormalization.NormalizeCategoryName(name);
        if (await db.Categories.AnyAsync(x => x.NormalizedName == normalized, cancellationToken))
            return (null, new("duplicate", "A category with that name already exists."));
        var now = DateTime.UtcNow;
        var category = new Category { Id = Guid.NewGuid(), Name = name, NormalizedName = normalized, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.Categories.Add(category);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (InventoryServiceSupport.IsUniqueViolation(ex, "IX_Categories_NormalizedName"))
        { return (null, new("duplicate", "A category with that name already exists.")); }
        return (new(category.Id, category.Name, category.IsActive, category.CreatedAtUtc, category.UpdatedAtUtc), null);
    }

    public async Task<(CategoryResponse? Response, ServiceError? Error)> UpdateAsync(Guid id, CategoryWriteRequest request, CancellationToken cancellationToken)
    {
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null) return (null, new("not_found", "Category was not found."));
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return (null, new("validation", "Name is required."));
        var normalized = InventoryNormalization.NormalizeCategoryName(name);
        if (await db.Categories.AnyAsync(x => x.Id != id && x.NormalizedName == normalized, cancellationToken))
            return (null, new("duplicate", "A category with that name already exists."));
        category.Name = name; category.NormalizedName = normalized; category.UpdatedAtUtc = DateTime.UtcNow;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (InventoryServiceSupport.IsUniqueViolation(ex, "IX_Categories_NormalizedName"))
        { return (null, new("duplicate", "A category with that name already exists.")); }
        return (new(category.Id, category.Name, category.IsActive, category.CreatedAtUtc, category.UpdatedAtUtc), null);
    }

    public async Task<ServiceError?> SetStatusAsync(Guid id, bool? isActive, CancellationToken cancellationToken)
    {
        if (!isActive.HasValue) return new("validation", "isActive is required.");
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null) return new("not_found", "Category was not found.");
        if (!isActive.Value && await db.Products.AnyAsync(x => x.CategoryId == id && x.IsActive, cancellationToken))
            return new("conflict", "A category with active products cannot be deactivated.");
        category.IsActive = isActive.Value; category.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return null;
    }
}
