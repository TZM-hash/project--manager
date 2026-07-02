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
        DateOnly? to)
    {
        Logs = logs;
        ActionOptions = actionOptions;
        Keyword = keyword;
        Action = action;
        From = from;
        To = to;
    }

    public IReadOnlyList<AuditLogDisplayModel> Logs { get; }

    public IReadOnlyList<AuditActionOption> ActionOptions { get; }

    public string? Keyword { get; }

    public string? Action { get; }

    public DateOnly? From { get; }

    public DateOnly? To { get; }
}
