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
        return await GetOrdersPageAsync(
            new MaintenanceOrderFilter(null, null, null, null, null, null),
            pageNumber,
            pageSize,
            cancellationToken);
    }

    public async Task<PagedResult<MaintenanceOrder>> GetOrdersPageAsync(
        MaintenanceOrderFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var orderedQuery = ApplyFilter(BaseQuery(), filter)
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
        string contractNumber,
        string customerName,
        string siteName,
        DateOnly startDate,
        DateOnly endDate,
        MaintenanceMethod method,
        int onSiteCount,
        int remoteCount,
        string onSiteSoftwareFrequency,
        string onSiteHardwareFrequency,
        string? executorUserId,
        decimal progressPercent,
        decimal handoverPercent,
        string maintenanceDescription,
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
        order.ContractNumber = contractNumber;
        order.CustomerName = customerName;
        order.SiteName = siteName;
        order.MaintenanceStartDate = startDate;
        order.MaintenanceEndDate = endDate;
        order.MaintenanceMethod = method;
        order.OnSiteAnnualCount = onSiteCount;
        order.RemoteAnnualCount = remoteCount;
        order.OnSiteSoftwareFrequency = onSiteSoftwareFrequency;
        order.OnSiteHardwareFrequency = onSiteHardwareFrequency;
        order.ExecutorUserId = executorUserId;
        order.ProgressPercent = progressPercent;
        order.HandoverPercent = handoverPercent;
        order.MaintenanceDescription = maintenanceDescription;
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

    private static IQueryable<MaintenanceOrder> ApplyFilter(
        IQueryable<MaintenanceOrder> query,
        MaintenanceOrderFilter filter)
    {
        if (filter.Year is not null)
        {
            query = query.Where(x => x.Year == filter.Year);
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
        {
            query = query.Where(x => x.CustomerName.Contains(filter.CustomerName));
        }

        if (filter.Method is not null)
        {
            query = query.Where(x => x.MaintenanceMethod == filter.Method);
        }

        if (!string.IsNullOrWhiteSpace(filter.ExecutorUserId))
        {
            query = query.Where(x => x.ExecutorUserId == filter.ExecutorUserId);
        }

        if (filter.MinProgressPercent is not null)
        {
            query = query.Where(x => x.ProgressPercent >= filter.MinProgressPercent);
        }

        if (filter.MaxProgressPercent is not null)
        {
            query = query.Where(x => x.ProgressPercent <= filter.MaxProgressPercent);
        }

        return query;
    }
}

public sealed record MaintenanceOrderFilter(
    int? Year,
    string? CustomerName,
    MaintenanceMethod? Method,
    string? ExecutorUserId,
    decimal? MinProgressPercent,
    decimal? MaxProgressPercent);
