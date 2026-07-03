using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public sealed class PlanningProjectService(ApplicationDbContext db)
{
    public async Task<IReadOnlyList<PlanningProject>> GetPlanningProjectsAsync(
        CancellationToken cancellationToken)
    {
        return await OrderForList(BaseQuery())
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<PlanningProject>> GetPlanningProjectsPageAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return await GetPlanningProjectsPageAsync(
            new PlanningProjectFilter(null, null, null),
            pageNumber,
            pageSize,
            cancellationToken);
    }

    public async Task<PagedResult<PlanningProject>> GetPlanningProjectsPageAsync(
        PlanningProjectFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var orderedQuery = OrderForList(ApplyFilter(BaseQuery(), filter));

        return await PagedResult<PlanningProject>.CreateAsync(
            orderedQuery,
            pageNumber,
            pageSize,
            cancellationToken);
    }

    public async Task<PlanningProject?> GetPlanningProjectAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await BaseQuery()
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<PlanningProject>> GetPlanningProjectsByIdsAsync(
        int[] ids,
        CancellationToken cancellationToken)
    {
        var query = BaseQuery()
            .Where(x => ids.Contains(x.Id));

        return await OrderForList(query)
            .ToListAsync(cancellationToken);
    }

    public async Task<PlanningProject> CreateAsync(
        PlanningProject project,
        CancellationToken cancellationToken)
    {
        project.IsDeleted = false;
        project.CreatedAt = DateTimeOffset.UtcNow;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        db.PlanningProjects.Add(project);
        await db.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task<PlanningProject?> UpdateAsync(
        int id,
        string name,
        string? leaderUserId,
        string? vendor,
        string? latestDescription,
        int? recordYear,
        int? recordMonth,
        string? currentRecord,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        var project = await db.PlanningProjects
            .Include(x => x.HistoryRecords)
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (project is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        project.Name = name;
        project.LeaderUserId = leaderUserId;
        project.Vendor = string.IsNullOrWhiteSpace(vendor) ? null : vendor.Trim();

        var normalizedCurrentRecord = RichTextSanitizer.Normalize(currentRecord);
        var normalizedLatestDescription = RichTextSanitizer.Normalize(latestDescription);

        if (recordYear.HasValue && recordMonth.HasValue && !string.IsNullOrWhiteSpace(normalizedCurrentRecord))
        {
            project.HistoryRecords.Add(new PlanningProjectHistoryRecord
            {
                Year = recordYear.Value,
                Month = recordMonth.Value,
                PreviousDescription = project.LatestDescription,
                CurrentRecord = normalizedCurrentRecord,
                CreatedByUserId = currentUserId,
                CreatedAt = now
            });
            project.LatestDescription = normalizedCurrentRecord;
        }
        else if (latestDescription is not null)
        {
            project.LatestDescription = normalizedLatestDescription;
        }

        project.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var project = await db.PlanningProjects
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (project is null)
        {
            return false;
        }

        project.IsDeleted = true;
        project.UpdatedAt = DateTimeOffset.UtcNow;
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
        var projects = await db.PlanningProjects
            .Where(x => idSet.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var project in projects)
        {
            project.IsDeleted = true;
            project.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return projects.Count;
    }

    public async Task<int> ImportAsync(
        IEnumerable<PlanningProject> projects,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var list = projects.ToList();
        foreach (var project in list)
        {
            project.IsDeleted = false;
            project.CreatedAt = now;
            project.UpdatedAt = now;
            db.PlanningProjects.Add(project);
        }

        await db.SaveChangesAsync(cancellationToken);
        return list.Count;
    }

    private IQueryable<PlanningProject> BaseQuery()
    {
        return db.PlanningProjects
            .AsNoTracking()
            .Include(x => x.Leader)
            .Include(x => x.HistoryRecords.OrderByDescending(r => r.Year).ThenByDescending(r => r.Month))
            .ThenInclude(x => x.CreatedByUser)
            .Where(x => !x.IsDeleted);
    }

    private IQueryable<PlanningProject> OrderForList(IQueryable<PlanningProject> query)
    {
        // SQLite 测试库不支持 DateTimeOffset 排序；正式 SQL Server 仍按最近更新展示。
        return db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? query.OrderByDescending(x => x.Id).ThenBy(x => x.Name)
            : query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Name);
    }

    private static IQueryable<PlanningProject> ApplyFilter(
        IQueryable<PlanningProject> query,
        PlanningProjectFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            query = query.Where(x => x.Name.Contains(filter.Name));
        }

        if (!string.IsNullOrWhiteSpace(filter.LeaderUserId))
        {
            query = query.Where(x => x.LeaderUserId != null && x.LeaderUserId.Contains(filter.LeaderUserId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Vendor))
        {
            query = query.Where(x => x.Vendor != null && x.Vendor.Contains(filter.Vendor));
        }

        return query;
    }
}

public sealed record PlanningProjectFilter(
    string? Name,
    string? LeaderUserId,
    string? Vendor);
