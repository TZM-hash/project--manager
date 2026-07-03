using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public sealed class WorkbenchProjectService(
    ApplicationDbContext db,
    AuditLogService auditLogService)
{
    public async Task<IReadOnlyList<Project>> GetProjectsForUserAsync(
        string userId,
        bool canViewAll,
        CancellationToken cancellationToken)
    {
        var query = BaseProjectQuery();

        if (!canViewAll)
        {
            query = query.Where(x => x.Assignments.Any(a => a.UserId == userId));
        }

        return await query
            .OrderByDescending(x => x.Year)
            .ThenBy(x => x.ProjectNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Project>> GetProjectsForUserPageAsync(
        string userId,
        bool canViewAll,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = BaseProjectQuery();

        if (!canViewAll)
        {
            query = query.Where(x => x.Assignments.Any(a => a.UserId == userId));
        }

        var orderedQuery = query
            .OrderByDescending(x => x.Year)
            .ThenBy(x => x.ProjectNumber);

        return await PagedResult<Project>.CreateAsync(
            orderedQuery,
            pageNumber,
            pageSize,
            cancellationToken);
    }

    public async Task<Project?> GetProjectForUserAsync(
        int projectId,
        string userId,
        bool canViewAll,
        CancellationToken cancellationToken)
    {
        var query = BaseProjectQuery().Where(x => x.Id == projectId);

        if (!canViewAll)
        {
            query = query.Where(x => x.Assignments.Any(a => a.UserId == userId));
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<UpdateProgressResult> UpdateProgressAsync(
        UpdateProgressRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (request.ProgressPercent < 0 || request.ProgressPercent > 100)
        {
            errors.Add("Progress percent must be between 0 and 100.");
        }

        var project = await db.Projects
            .Include(x => x.Assignments)
            .SingleOrDefaultAsync(x => !x.IsDeleted && x.Id == request.ProjectId, cancellationToken);

        if (project is null)
        {
            errors.Add("Project was not found.");
            return new UpdateProgressResult(false, errors);
        }

        if (!request.CanEditAll && project.Assignments.All(x => x.UserId != request.UserId))
        {
            errors.Add("Project is not assigned to the current user.");
        }

        if (errors.Count > 0)
        {
            return new UpdateProgressResult(false, errors);
        }

        // 工作台只能改进度相关字段，因此审计明细也只保留进度和进度说明。
        var before = ProjectAuditChangeBuilder.CreateSnapshot(project);
        project.ProgressPercent = request.ProgressPercent;
        project.ProgressDescription = RichTextSanitizer.Normalize(request.ProgressDescription);
        project.UpdatedByUserId = request.UserId;
        project.UpdatedAt = DateTimeOffset.UtcNow;
        var after = ProjectAuditChangeBuilder.CreateSnapshot(project);
        var changes = ProjectAuditChangeBuilder.BuildUpdateChanges(before, after)
            .Where(x => x.Label is "项目进度" or "进度说明")
            .ToList();

        await db.SaveChangesAsync(cancellationToken);
        if (changes.Count > 0)
        {
            await auditLogService.LogProjectChangeAsync(
                request.UserId,
                "ProgressUpdate",
                project.Id,
                project.ProjectNumber,
                $"更新进度 {project.ProjectNumber}",
                changes,
                cancellationToken);
        }

        return new UpdateProgressResult(true, []);
    }

    private IQueryable<Project> BaseProjectQuery()
    {
        return db.Projects
            .AsNoTracking()
            .Include(x => x.Status)
            .ThenInclude(x => x!.Style)
            .Include(x => x.Assignments)
            .ThenInclude(x => x.User)
            .Include(x => x.PurchaseRequests.Where(p => !p.IsDeleted))
            .ThenInclude(x => x.PurchaseStaff)
            .Include(x => x.PurchaseRequests.Where(p => !p.IsDeleted))
            .ThenInclude(x => x.SubCaseContact)
            .Include(x => x.UpdatedByUser)
            .Where(x => !x.IsDeleted);
    }
}

public sealed record UpdateProgressRequest(
    int ProjectId,
    string UserId,
    bool CanEditAll,
    decimal ProgressPercent,
    string? ProgressDescription);

public sealed record UpdateProgressResult(bool Success, IReadOnlyList<string> Errors);
