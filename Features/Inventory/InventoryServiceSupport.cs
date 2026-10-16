using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Api.Features.Inventory;

public static class InventoryServiceSupport
{
    public static bool IsUniqueViolation(DbUpdateException exception, string indexName)
    {
        var sqlException = exception.GetBaseException() as SqlException;
        return sqlException is not null
            && sqlException.Number is 2601 or 2627
            && sqlException.Message.Contains(indexName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValidPagination(PaginationQuery query) =>
        query.Page >= 1 && query.PageSize is >= 1 and <= 100;

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
