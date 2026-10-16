using InventoryManagement.Api.Data;
using InventoryManagement.Api.Features.Inventory;
using InventoryManagement.Api.Inventory;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Api.Features.Customers;

public sealed class CustomerService(ApplicationDbContext db)
{
    public async Task<(PagedResponse<CustomerResponse>? Response, CustomerServiceError? Error)> GetAsync(CustomerQuery query, CancellationToken cancellationToken)
    {
        if (!InventoryServiceSupport.IsValidPagination(query)) return (null, new("validation", "Page must be at least 1 and pageSize must be between 1 and 100."));
        var search = query.Search?.Trim();
        var customers = db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            customers = customers.Where(x => x.Name.Contains(search) || (x.ContactName != null && x.ContactName.Contains(search)) || (x.Email != null && x.Email.Contains(search)));
        if (query.IsActive.HasValue) customers = customers.Where(x => x.IsActive == query.IsActive.Value);
        var total = await customers.CountAsync(cancellationToken);
        var rows = await customers.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CustomerResponse(x.Id, x.Name, x.ContactName, x.Email, x.Phone, x.Address, x.IsActive, Encode(x.RowVersion), x.CreatedAtUtc, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        return (new(rows, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize)), null);
    }

    public async Task<CustomerResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Customers.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new CustomerResponse(x.Id, x.Name, x.ContactName, x.Email, x.Phone, x.Address, x.IsActive, Encode(x.RowVersion), x.CreatedAtUtc, x.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<(CustomerResponse? Response, CustomerServiceError? Error)> CreateAsync(CustomerWriteRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return (null, new("validation", "Name is required."));
        var now = DateTime.UtcNow;
        var customer = new Customer { Id = Guid.NewGuid(), Name = name, ContactName = InventoryServiceSupport.NormalizeOptional(request.ContactName), Email = InventoryServiceSupport.NormalizeOptional(request.Email), Phone = InventoryServiceSupport.NormalizeOptional(request.Phone), Address = InventoryServiceSupport.NormalizeOptional(request.Address), CreatedAtUtc = now, UpdatedAtUtc = now };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(customer.Id, cancellationToken), null);
    }

    public async Task<(CustomerResponse? Response, CustomerServiceError? Error)> UpdateAsync(Guid id, CustomerUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!TryDecode(request.RowVersion, out var rowVersion)) return (null, new("invalid_row_version", "RowVersion must be a valid Base64 value."));
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null) return (null, new("not_found", "Customer was not found."));
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return (null, new("validation", "Name is required."));
        db.Entry(customer).Property(x => x.RowVersion).OriginalValue = rowVersion;
        customer.Name = name;
        customer.ContactName = InventoryServiceSupport.NormalizeOptional(request.ContactName);
        customer.Email = InventoryServiceSupport.NormalizeOptional(request.Email);
        customer.Phone = InventoryServiceSupport.NormalizeOptional(request.Phone);
        customer.Address = InventoryServiceSupport.NormalizeOptional(request.Address);
        customer.UpdatedAtUtc = DateTime.UtcNow;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return (null, new("concurrency", "The customer was changed by another request.")); }
        return (await GetByIdAsync(id, cancellationToken), null);
    }

    public async Task<CustomerServiceError?> SetStatusAsync(Guid id, CustomerStatusRequest request, CancellationToken cancellationToken)
    {
        if (!request.IsActive.HasValue) return new("validation", "isActive is required.");
        if (!TryDecode(request.RowVersion, out var rowVersion)) return new("invalid_row_version", "RowVersion must be a valid Base64 value.");
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null) return new("not_found", "Customer was not found.");
        db.Entry(customer).Property(x => x.RowVersion).OriginalValue = rowVersion;
        customer.IsActive = request.IsActive.Value;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new("concurrency", "The customer was changed by another request."); }
        return null;
    }

    private static bool TryDecode(string value, out byte[] bytes)
    {
        try { bytes = Convert.FromBase64String(value); return bytes.Length > 0; }
        catch (FormatException) { bytes = []; return false; }
    }

    private static string Encode(byte[] value) => Convert.ToBase64String(value);
}
