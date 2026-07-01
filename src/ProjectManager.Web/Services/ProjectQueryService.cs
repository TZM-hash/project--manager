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
            .Include(x => x.Status)
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

        if (filter.OpenOnly)
        {
            query = query.Where(x => x.Status != null && !x.Status.IsClosed);
        }

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
    bool OpenOnly);

public sealed record OpenProjectSummaryRow(
    string StatusName,
    int Count,
    decimal ProjectAmountTotal,
    decimal PurchaseAmountTotal,
    decimal ActualPaidAmountTotal);
