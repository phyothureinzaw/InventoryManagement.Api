using InventoryManagement.Api.Data;
using InventoryManagement.Api.Inventory;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Api.Features.StockMovements;

public sealed class DashboardService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    MovementReadService movementReadService)
{
    public async Task<(DashboardResponse? Response, HistoryServiceError? Error)> GetAsync(
        DashboardQuery query,
        CancellationToken cancellationToken)
    {
        if (!UtcRangeParser.TryParseRequired(query.FromUtc, query.ToUtc, out var range, out var rangeError))
            return (null, rangeError);
        if (range!.ToUtc - range.FromUtc > TimeSpan.FromDays(366))
            return (null, new("validation", "The dashboard date range cannot exceed 366 days."));

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var activeProducts = db.Products.AsNoTracking().Where(x => x.IsActive);
        var counts = await activeProducts.GroupBy(_ => 1)
            .Select(group => new
            {
                Active = group.Count(),
                Low = group.Count(x => x.CurrentStock <= x.LowStockThreshold),
                Out = group.Count(x => x.CurrentStock == 0),
            })
            .SingleOrDefaultAsync(cancellationToken);

        var dailyRows = await db.StockMovements.AsNoTracking()
            .Where(x => x.OccurredAtUtc >= range.FromUtc.UtcDateTime && x.OccurredAtUtc < range.ToUtc.UtcDateTime)
            .GroupBy(x => x.OccurredAtUtc.Date)
            .Select(group => new
            {
                Date = group.Key,
                StockInCount = group.Count(x => x.MovementType == MovementType.StockIn),
                StockOutCount = group.Count(x => x.MovementType == MovementType.StockOut),
            })
            .ToListAsync(cancellationToken);
        var dailyByDate = dailyRows.ToDictionary(x => DateOnly.FromDateTime(x.Date));
        var daily = new List<DailyActivityResponse>();
        var lastDate = DateOnly.FromDateTime((range.ToUtc - TimeSpan.FromTicks(1)).UtcDateTime);
        for (var date = DateOnly.FromDateTime(range.FromUtc.UtcDateTime); date <= lastDate; date = date.AddDays(1))
        {
            dailyByDate.TryGetValue(date, out var row);
            daily.Add(new(date, row?.StockInCount ?? 0, row?.StockOutCount ?? 0));
        }

        var latest = await movementReadService.GetLatestAsync(10, cancellationToken);
        return (new DashboardResponse(
            counts?.Active ?? 0,
            counts?.Low ?? 0,
            counts?.Out ?? 0,
            latest,
            daily), null);
    }
}
