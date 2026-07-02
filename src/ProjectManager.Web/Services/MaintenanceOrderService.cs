using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public sealed class MaintenanceOrderService(ApplicationDbContext db)
{
    public async Task<IReadOnlyList<MaintenanceOrder>> GetOrdersAsync(CancellationToken cancellationToken)
    {
        return await BaseQuery()
            .OrderByDescending(x => x.Year)
            .ThenBy(x => x.CustomerName)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<MaintenanceOrder>> GetOrdersPageAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var orderedQuery = BaseQuery()
            .OrderByDescending(x => x.Year)
            .ThenBy(x => x.CustomerName);

        return await PagedResult<MaintenanceOrder>.CreateAsync(
            orderedQuery,
            pageNumber,
            pageSize,
            cancellationToken);
    }

    public async Task<MaintenanceOrder?> GetOrderAsync(int id, CancellationToken cancellationToken)
    {
        return await BaseQuery()
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
    }

    public async Task<MaintenanceOrder> CreateAsync(MaintenanceOrder order, string? currentUserId, CancellationToken cancellationToken)
    {
        order.IsDeleted = false;
        order.CreatedAt = DateTimeOffset.UtcNow;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        order.UpdatedByUserId = currentUserId;
        db.MaintenanceOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task<MaintenanceOrder?> UpdateAsync(
        int id,
        int year,
        string customerName,
        DateOnly startDate,
        DateOnly endDate,
        MaintenanceMethod method,
        int onSiteCount,
        int remoteCount,
        string? executorUserId,
        decimal handoverPercent,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        var order = await db.MaintenanceOrders
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (order is null)
        {
            return null;
        }

        order.Year = year;
        order.CustomerName = customerName;
        order.MaintenanceStartDate = startDate;
        order.MaintenanceEndDate = endDate;
        order.MaintenanceMethod = method;
        order.OnSiteAnnualCount = onSiteCount;
        order.RemoteAnnualCount = remoteCount;
        order.ExecutorUserId = executorUserId;
        order.HandoverPercent = handoverPercent;
        order.UpdatedByUserId = currentUserId;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var order = await db.MaintenanceOrders
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (order is null)
        {
            return false;
        }

        order.IsDeleted = true;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeleteManyAsync(int[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return 0;
        }

        var idSet = ids.Distinct().ToArray();
        var orders = await db.MaintenanceOrders
            .Where(x => idSet.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var order in orders)
        {
            order.IsDeleted = true;
            order.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return orders.Count;
    }

    private IQueryable<MaintenanceOrder> BaseQuery()
    {
        return db.MaintenanceOrders
            .AsNoTracking()
            .Include(x => x.Executor)
            .Include(x => x.UpdatedByUser)
            .Where(x => !x.IsDeleted);
    }
}
