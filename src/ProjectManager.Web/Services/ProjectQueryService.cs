using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public sealed class ProjectQueryService(ApplicationDbContext db)
{
    public async Task<IReadOnlyList<Project>> GetProjectsAsync(
        ProjectFilter filter,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(BaseProjectQuery(), filter);
        return await query
            .OrderByDescending(x => x.Year)
            .ThenBy(x => x.ProjectNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Project> Items, int TotalCount)> GetProjectsAsync(
        ProjectFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var pageResult = await GetProjectsPageAsync(filter, page, pageSize, cancellationToken);
        return (pageResult.Items, pageResult.TotalCount);
    }

    public async Task<PagedResult<Project>> GetProjectsPageAsync(
        ProjectFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(BaseProjectQuery(), filter);
        var orderedQuery = query
            .OrderByDescending(x => x.Year)
            .ThenBy(x => x.ProjectNumber);

        return await PagedResult<Project>.CreateAsync(
            orderedQuery,
            pageNumber,
            pageSize,
            cancellationToken);
    }

    public async Task<IReadOnlyList<OpenProjectSummaryRow>> GetOpenProjectSummaryAsync(
        ProjectFilter filter,
        CancellationToken cancellationToken)
    {
        var projects = await GetProjectsAsync(filter with { OpenOnly = true }, cancellationToken);

        return projects
            .GroupBy(x => x.Status?.Name ?? string.Empty)
            .OrderBy(x => x.Key)
            .Select(group => new OpenProjectSummaryRow(
                group.Key,
                group.Count(),
                group.Sum(x => x.ProjectAmount),
                group.SelectMany(x => x.PurchaseRequests.Where(p => !p.IsDeleted)).Sum(x => x.PurchaseAmount),
                group.SelectMany(x => x.PurchaseRequests.Where(p => !p.IsDeleted)).Sum(x => x.ActualPaidAmount)))
            .ToList();
    }

    private IQueryable<Project> BaseProjectQuery()
    {
        return db.Projects
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Status)
            .ThenInclude(x => x!.Style)
            .Include(x => x.Assignments)
            .ThenInclude(x => x.User)
            .Include(x => x.PurchaseRequests.Where(p => !p.IsDeleted))
            .ThenInclude(x => x.PurchaseStaff)
            .Include(x => x.PurchaseRequests.Where(p => !p.IsDeleted))
            .ThenInclude(x => x.SubCaseContact)
            .Where(x => !x.IsDeleted);
    }

    private static IQueryable<Project> ApplyFilters(IQueryable<Project> query, ProjectFilter filter)
    {
        if (filter.Year is not null)
        {
            query = query.Where(x => x.Year == filter.Year);
        }

        if (!string.IsNullOrWhiteSpace(filter.ParentCaseNumber))
        {
            query = query.Where(x => x.ParentCaseNumber != null &&
                                     x.ParentCaseNumber.Contains(filter.ParentCaseNumber));
        }

        if (!string.IsNullOrWhiteSpace(filter.ProjectNumber))
        {
            query = query.Where(x => x.ProjectNumber.Contains(filter.ProjectNumber));
        }

        if (!string.IsNullOrWhiteSpace(filter.ProjectName))
        {
            query = query.Where(x => x.Name.Contains(filter.ProjectName));
        }

        if (!string.IsNullOrWhiteSpace(filter.PersonnelUserId))
        {
            query = query.Where(x => x.Assignments.Any(a => a.UserId == filter.PersonnelUserId));
        }

        if (filter.StatusId is not null)
        {
            query = query.Where(x => x.StatusId == filter.StatusId);
        }

        if (filter.ProjectType is not null)
        {
            query = query.Where(x => x.ProjectType == filter.ProjectType);
        }

        if (filter.OpenOnly)
        {
            query = query.Where(x => x.Status != null && !x.Status.IsClosed);
        }

        query = filter.AnalysisType switch
        {
            ProjectAnalysisTypes.LowProgress => query.Where(x => x.ProgressPercent < 30),
            ProjectAnalysisTypes.CollectionLag => query.Where(x => x.CollectionPercent + 25 < x.ProgressPercent),
            ProjectAnalysisTypes.StaleUpdate => query.Where(x => x.UpdatedAt < DateTimeOffset.UtcNow.AddDays(-30)),
            _ => query
        };

        return query;
    }
}

public sealed record ProjectFilter(
    int? Year,
    string? ParentCaseNumber,
    string? ProjectNumber,
    string? ProjectName,
    string? PersonnelUserId,
    int? StatusId,
    bool OpenOnly,
    string? AnalysisType = null,
    ProjectType? ProjectType = null);

public static class ProjectAnalysisTypes
{
    public const string LowProgress = "low-progress";
    public const string CollectionLag = "collection-lag";
    public const string StaleUpdate = "stale-update";
    public const string Overdue = "overdue";
    public const string Pending = "pending";
    public const string Upcoming = "upcoming";
}

public sealed record OpenProjectSummaryRow(
    string StatusName,
    int Count,
    decimal ProjectAmountTotal,
    decimal PurchaseAmountTotal,
    decimal ActualPaidAmountTotal);
