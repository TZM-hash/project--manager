namespace ProjectManager.Web.Pages.Shared;

public sealed record AuditActionOption(string Value, string Text);

public sealed class AuditTrailViewModel
{
    public AuditTrailViewModel(
        IReadOnlyList<AuditLogDisplayModel> logs,
        IReadOnlyList<AuditActionOption> actionOptions,
        string? keyword,
        string? action,
        DateOnly? from,
        DateOnly? to,
        int pageNumber = 1,
        int pageSize = 25,
        int totalCount = 0)
    {
        Logs = logs;
        ActionOptions = actionOptions;
        Keyword = keyword;
        Action = action;
        From = from;
        To = to;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    public IReadOnlyList<AuditLogDisplayModel> Logs { get; }

    public IReadOnlyList<AuditActionOption> ActionOptions { get; }

    public string? Keyword { get; }

    public string? Action { get; }

    public DateOnly? From { get; }

    public DateOnly? To { get; }

    public int PageNumber { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

    public int FirstItemNumber => TotalCount == 0 ? 0 : (PageNumber - 1) * PageSize + 1;

    public int LastItemNumber => Math.Min(PageNumber * PageSize, TotalCount);
}
