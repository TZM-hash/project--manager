using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public class ProjectArchiveService
{
    private readonly ApplicationDbContext dbContext;
    private readonly TimeProvider timeProvider;

    public ProjectArchiveService(
        ApplicationDbContext dbContext,
        TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
    }

    public async Task<bool> CanArchiveAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .Include(x => x.Status)
            .Include(x => x.Assignments)
            .ThenInclude(a => a.User)
            .FirstOrDefaultAsync(x => x.Id == projectId && !x.IsDeleted, cancellationToken);

        if (project is null)
        {
            return false;
        }

        return CanArchiveProject(project);
    }

    public bool CanArchiveProject(Project project)
    {
        if (project.Status is null || !project.Status.IsClosed)
        {
            return false;
        }

        if (!project.ClosedYearMonth.HasValue)
        {
            return false;
        }

        var now = timeProvider.GetLocalNow();
        var currentYearMonth = new DateOnly(now.Year, now.Month, 1);
        var closedPlusTwo = project.ClosedYearMonth.Value.AddMonths(2);

        return currentYearMonth >= closedPlusTwo;
    }

    public async Task<(bool Success, string? Error)> ArchiveProjectAsync(
        int projectId,
        string? archivedByUserId,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .Include(x => x.Status)
            .Include(x => x.Assignments)
            .ThenInclude(a => a.User)
            .FirstOrDefaultAsync(x => x.Id == projectId && !x.IsDeleted, cancellationToken);

        if (project is null)
        {
            return (false, "專案不存在或已刪除");
        }

        if (!CanArchiveProject(project))
        {
            return (false, "不符合歸檔條件：專案狀態需為已結案，且結案月份需超過2個月");
        }

        var assignmentSummary = string.Join(
            "；",
            project.Assignments.Select(x => x.User?.DisplayName ?? x.User?.UserName ?? x.UserId));

        var assignmentUserIds = string.Join(
            ",",
            project.Assignments.Select(x => x.UserId));

        var archive = new ProjectArchive
        {
            OriginalProjectId = project.Id,
            Year = project.Year,
            ProjectType = project.ProjectType,
            ParentCaseNumber = project.ParentCaseNumber,
            ProjectNumber = project.ProjectNumber,
            Name = project.Name,
            ProgressPercent = project.ProgressPercent,
            ProjectAmount = project.ProjectAmount,
            CollectionPercent = project.CollectionPercent,
            ProgressDescription = project.ProgressDescription,
            StatusName = project.Status?.Name ?? string.Empty,
            StatusIsClosed = project.Status?.IsClosed ?? false,
            ClosedYearMonth = project.ClosedYearMonth,
            ArchivedByUserId = archivedByUserId,
            ArchivedAt = timeProvider.GetUtcNow(),
            OriginalCreatedAt = project.CreatedAt,
            OriginalUpdatedAt = project.UpdatedAt,
            AssignmentSummary = assignmentSummary,
            AssignmentUserIds = assignmentUserIds
        };

        dbContext.ProjectArchives.Add(archive);
        dbContext.Projects.Remove(project);

        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, null);
    }

    public bool CanRestoreProjectArchive(ProjectArchive archive)
    {
        if (!archive.ClosedYearMonth.HasValue)
        {
            return false;
        }

        var now = timeProvider.GetLocalNow();
        var currentYearMonth = new DateOnly(now.Year, now.Month, 1);
        var closedPlusSix = archive.ClosedYearMonth.Value.AddMonths(6);

        return currentYearMonth <= closedPlusSix;
    }

    public async Task<(bool Success, string? Error)> RestoreProjectAsync(
        int archiveId,
        string? restoredByUserId,
        CancellationToken cancellationToken)
    {
        var archive = await dbContext.ProjectArchives
            .FirstOrDefaultAsync(x => x.Id == archiveId, cancellationToken);

        if (archive is null)
        {
            return (false, "歸檔記錄不存在");
        }

        if (!CanRestoreProjectArchive(archive))
        {
            return (false, "不符合還原條件：結案日期需在半年內");
        }

        // 查找已結案狀態
        var closedStatus = await dbContext.ProjectStatuses
            .FirstOrDefaultAsync(x => x.IsClosed, cancellationToken);

        if (closedStatus is null)
        {
            return (false, "系統中沒有已結案狀態，無法還原");
        }

        var project = new Project
        {
            Year = archive.Year,
            ProjectType = archive.ProjectType,
            ParentCaseNumber = archive.ParentCaseNumber,
            ProjectNumber = archive.ProjectNumber,
            Name = archive.Name,
            ProgressPercent = archive.ProgressPercent,
            ProjectAmount = archive.ProjectAmount,
            CollectionPercent = archive.CollectionPercent,
            ProgressDescription = archive.ProgressDescription,
            StatusId = closedStatus.Id,
            ClosedYearMonth = archive.ClosedYearMonth,
            CreatedAt = archive.OriginalCreatedAt,
            UpdatedAt = timeProvider.GetUtcNow(),
            UpdatedByUserId = restoredByUserId
        };

        dbContext.Projects.Add(project);

        // 還原經辦人員指派關係
        if (!string.IsNullOrWhiteSpace(archive.AssignmentUserIds))
        {
            var userIds = archive.AssignmentUserIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            foreach (var userId in userIds)
            {
                dbContext.ProjectAssignments.Add(new ProjectAssignment
                {
                    Project = project,
                    UserId = userId
                });
            }
        }

        dbContext.ProjectArchives.Remove(archive);

        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, null);
    }
}
