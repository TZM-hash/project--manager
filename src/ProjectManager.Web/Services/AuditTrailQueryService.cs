using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Pages.Shared;

namespace ProjectManager.Web.Services;

public sealed class AuditTrailQueryService(ApplicationDbContext db)
{
    public const int DefaultPageSize = 25;

    public static readonly int[] AllowedPageSizes = [10, 25, 50, 100];

    public async Task<AuditTrailViewModel> BuildAsync(
        int projectId,
        string? keyword,
        string? action,
        DateOnly? from,
        DateOnly? to,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var projectIdText = projectId.ToString(CultureInfo.InvariantCulture);
        var projectLogs = db.AuditLogs
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId ||
                        (x.EntityName == "Project" && x.EntityId == projectIdText));

        var actionValues = await projectLogs
            .Where(x => x.Action != "")
            .Select(x => x.Action)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var filtered = projectLogs;
        if (!string.IsNullOrWhiteSpace(action))
        {
            filtered = filtered.Where(x => x.Action == action);
        }

        if (from is not null)
        {
            var fromUtc = ToUtcBoundary(from.Value);
            filtered = filtered.Where(x => x.CreatedAt >= fromUtc);
        }

        if (to is not null)
        {
            var toExclusiveUtc = ToUtcBoundary(to.Value.AddDays(1));
            filtered = filtered.Where(x => x.CreatedAt < toExclusiveUtc);
        }

        var normalizedKeyword = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            filtered = filtered.Where(x =>
                (x.User != null &&
                 ((x.User.DisplayName != null && x.User.DisplayName.Contains(normalizedKeyword)) ||
                  (x.User.UserName != null && x.User.UserName.Contains(normalizedKeyword)) ||
                  (x.User.Email != null && x.User.Email.Contains(normalizedKeyword)))) ||
                x.Action.Contains(normalizedKeyword) ||
                x.Description.Contains(normalizedKeyword) ||
                (x.ChangeSummary != null && x.ChangeSummary.Contains(normalizedKeyword)) ||
                (x.ChangeDetailsJson != null && x.ChangeDetailsJson.Contains(normalizedKeyword)));
        }

        var selectedPageSize = AllowedPageSizes.Contains(pageSize) ? pageSize : DefaultPageSize;
        var totalCount = await filtered.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)selectedPageSize));
        var currentPage = Math.Clamp(pageNumber, 1, totalPages);
        var logs = await filtered
            .Include(x => x.User)
            .OrderByDescending(x => x.Id)
            .Skip((currentPage - 1) * selectedPageSize)
            .Take(selectedPageSize)
            .ToListAsync(cancellationToken);

        return new AuditTrailViewModel(
            AuditLogDisplayModel.FromLogs(logs),
            actionValues
                .Select(value => new AuditActionOption(value, AuditLogDisplayModel.GetActionLabel(value)))
                .OrderBy(x => x.Text)
                .ToList(),
            normalizedKeyword,
            action,
            from,
            to,
            currentPage,
            selectedPageSize,
            totalCount);
    }

    private static DateTimeOffset ToUtcBoundary(DateOnly date)
    {
        var localDateTime = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var offset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset).ToUniversalTime();
    }
}
