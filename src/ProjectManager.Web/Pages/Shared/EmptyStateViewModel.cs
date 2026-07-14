namespace ProjectManager.Web.Pages.Shared;

public sealed record EmptyStateViewModel(
    string Title,
    string Description,
    string Icon = "inbox",
    string? PrimaryActionText = null,
    string? PrimaryActionPage = null,
    Dictionary<string, string?>? PrimaryActionRouteValues = null,
    string? ClearActionText = null,
    string? ClearActionPage = null,
    Dictionary<string, string?>? ClearActionRouteValues = null);
