using InventoryManagement.Api.Data;
using InventoryManagement.Api.Inventory;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Api.Features.Inventory;

public sealed class SupplierService(ApplicationDbContext db)
{
    public async Task<PagedResponse<SupplierResponse>?> GetAsync(SupplierQuery query, CancellationToken cancellationToken)
    {
        if (!InventoryServiceSupport.IsValidPagination(query)) return null;
        var search = query.Search?.Trim();
        var suppliers = db.Suppliers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            suppliers = suppliers.Where(x => x.Name.Contains(search) || (x.ContactName != null && x.ContactName.Contains(search)) || (x.Email != null && x.Email.Contains(search)));
        if (query.IsActive.HasValue) suppliers = suppliers.Where(x => x.IsActive == query.IsActive.Value);
        var total = await suppliers.CountAsync(cancellationToken);
        var items = await suppliers.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new SupplierResponse(x.Id, x.Name, x.ContactName, x.Phone, x.Email, x.Address, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        return new(items, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public async Task<SupplierResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Suppliers.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new SupplierResponse(x.Id, x.Name, x.ContactName, x.Phone, x.Email, x.Address, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<(SupplierResponse? Response, ServiceError? Error)> CreateAsync(SupplierWriteRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return (null, new("validation", "Name is required."));
        var now = DateTime.UtcNow;
        var supplier = new Supplier { Id = Guid.NewGuid(), Name = name, ContactName = InventoryServiceSupport.NormalizeOptional(request.ContactName), Phone = InventoryServiceSupport.NormalizeOptional(request.Phone), Email = InventoryServiceSupport.NormalizeOptional(request.Email), Address = InventoryServiceSupport.NormalizeOptional(request.Address), CreatedAtUtc = now, UpdatedAtUtc = now };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);
        return (new(supplier.Id, supplier.Name, supplier.ContactName, supplier.Phone, supplier.Email, supplier.Address, supplier.IsActive, supplier.CreatedAtUtc, supplier.UpdatedAtUtc), null);
    }

    public async Task<(SupplierResponse? Response, ServiceError? Error)> UpdateAsync(Guid id, SupplierWriteRequest request, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (supplier is null) return (null, new("not_found", "Supplier was not found."));
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return (null, new("validation", "Name is required."));
        supplier.Name = name; supplier.ContactName = InventoryServiceSupport.NormalizeOptional(request.ContactName); supplier.Phone = InventoryServiceSupport.NormalizeOptional(request.Phone); supplier.Email = InventoryServiceSupport.NormalizeOptional(request.Email); supplier.Address = InventoryServiceSupport.NormalizeOptional(request.Address); supplier.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return (new(supplier.Id, supplier.Name, supplier.ContactName, supplier.Phone, supplier.Email, supplier.Address, supplier.IsActive, supplier.CreatedAtUtc, supplier.UpdatedAtUtc), null);
    }

    public async Task<ServiceError?> SetStatusAsync(Guid id, bool? isActive, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (supplier is null) return new("not_found", "Supplier was not found.");
        if (!isActive.HasValue) return new("validation", "isActive is required.");
        supplier.IsActive = isActive.Value; supplier.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return null;
    }
}
