using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public sealed class ProjectCollaborationService(ApplicationDbContext db)
{
    public async Task<CollaborationPage> GetPageAsync(
        int projectId,
        string userId,
        bool canViewAll,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessProjectAsync(projectId, userId, canViewAll, cancellationToken))
        {
            return new CollaborationPage(false, [], 0, 1, Math.Clamp(pageSize, 5, 50), 1);
        }

        pageSize = Math.Clamp(pageSize, 5, 50);
        var query = db.ProjectCollaborationRecords
            .AsNoTracking()
            .Include(record => record.CreatedByUser)
            .Where(record => record.ProjectId == projectId);
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        pageNumber = Math.Clamp(pageNumber, 1, totalPages);
        var items = await query
            .OrderByDescending(record => record.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new CollaborationPage(true, items, totalCount, pageNumber, pageSize, totalPages);
    }

    public async Task<CollaborationResult> AddAsync(
        CollaborationCommand command,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessProjectAsync(command.ProjectId, command.UserId, command.CanViewAll, cancellationToken))
        {
            return Failure("您沒有權限存取此專案的協作記錄。");
        }

        var errors = Validate(command);
        if (errors.Count > 0)
        {
            return new CollaborationResult(false, errors, false, null);
        }

        var now = DateTimeOffset.UtcNow;
        var record = new ProjectCollaborationRecord
        {
            ProjectId = command.ProjectId,
            Category = NormalizeCategory(command.Category),
            Content = NormalizeContent(command.Content),
            CreatedByUserId = command.UserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.ProjectCollaborationRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return new CollaborationResult(true, [], false, record);
    }

    public async Task<CollaborationResult> UpdateAsync(
        CollaborationCommand command,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessProjectAsync(command.ProjectId, command.UserId, command.CanViewAll, cancellationToken))
        {
            return Failure("您沒有權限存取此專案的協作記錄。");
        }

        var errors = Validate(command);
        if (errors.Count > 0)
        {
            return new CollaborationResult(false, errors, false, null);
        }

        var record = await db.ProjectCollaborationRecords.SingleOrDefaultAsync(
            item => item.ProjectId == command.ProjectId && item.Id == command.RecordId,
            cancellationToken);
        if (record is null)
        {
            return Failure("找不到協作記錄。");
        }

        if (!command.CanEditAll && record.CreatedByUserId != command.UserId)
        {
            return Failure("您只能修改自己建立的協作記錄。");
        }

        if (!TryApplyRowVersion(record, command.RowVersion, out var versionError))
        {
            return new CollaborationResult(false, [versionError], true, null);
        }

        record.Category = NormalizeCategory(command.Category);
        record.Content = NormalizeContent(command.Content);
        record.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new CollaborationResult(true, [], false, record);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
    }

    public async Task<CollaborationResult> DeleteAsync(
        CollaborationCommand command,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessProjectAsync(command.ProjectId, command.UserId, command.CanViewAll, cancellationToken))
        {
            return Failure("您沒有權限存取此專案的協作記錄。");
        }

        var record = await db.ProjectCollaborationRecords.SingleOrDefaultAsync(
            item => item.ProjectId == command.ProjectId && item.Id == command.RecordId,
            cancellationToken);
        if (record is null)
        {
            return Failure("找不到協作記錄。");
        }

        if (!command.CanEditAll && record.CreatedByUserId != command.UserId)
        {
            return Failure("您只能刪除自己建立的協作記錄。");
        }

        if (!TryApplyRowVersion(record, command.RowVersion, out var versionError))
        {
            return new CollaborationResult(false, [versionError], true, null);
        }

        db.ProjectCollaborationRecords.Remove(record);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new CollaborationResult(true, [], false, record);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
    }

    private Task<bool> CanAccessProjectAsync(
        int projectId,
        string userId,
        bool canViewAll,
        CancellationToken cancellationToken) =>
        db.Projects.AnyAsync(project =>
            !project.IsDeleted &&
            project.Id == projectId &&
            (canViewAll || project.Assignments.Any(assignment => assignment.UserId == userId)),
            cancellationToken);

    private bool TryApplyRowVersion(
        ProjectCollaborationRecord record,
        string? rowVersion,
        out string error)
    {
        try
        {
            db.Entry(record).Property(item => item.RowVersion).OriginalValue =
                Convert.FromBase64String(rowVersion ?? string.Empty);
            error = string.Empty;
            return true;
        }
        catch (FormatException)
        {
            error = "協作資料版本無效，請重新載入後再試。";
            return false;
        }
    }

    private static List<string> Validate(CollaborationCommand command)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(command.Content))
        {
            errors.Add("請輸入協作內容。");
        }
        else if (NormalizeContent(command.Content).Length > 4000)
        {
            errors.Add("協作內容最多 4000 個字元。");
        }

        return errors;
    }

    private static string NormalizeCategory(string? category)
    {
        var value = string.IsNullOrWhiteSpace(category) ? "進度協作" : category.Trim();
        return value.Length <= 50 ? value : value[..50];
    }

    private static string NormalizeContent(string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        return new string(normalized.Where(character => character is '\n' or '\t' || !char.IsControl(character)).ToArray());
    }

    private static CollaborationResult Failure(string error) => new(false, [error], false, null);

    private static CollaborationResult Conflict() => new(
        false,
        ["此協作記錄已被其他使用者更新，您的內容尚未覆蓋新資料。請重新載入後再送出。"],
        true,
        null);
}

public sealed record CollaborationCommand(
    int ProjectId,
    int? RecordId,
    string UserId,
    bool CanViewAll,
    bool CanEditAll,
    string? Category,
    string Content,
    string? RowVersion);

public sealed record CollaborationResult(
    bool Success,
    IReadOnlyList<string> Errors,
    bool IsConcurrencyConflict,
    ProjectCollaborationRecord? Record);

public sealed record CollaborationPage(
    bool CanAccess,
    IReadOnlyList<ProjectCollaborationRecord> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);
