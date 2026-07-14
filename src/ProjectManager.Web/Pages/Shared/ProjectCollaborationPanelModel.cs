using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Shared;

public sealed record ProjectCollaborationPanelModel(
    Project Project,
    CollaborationPage Page,
    ProjectCollaborationInputModel Input,
    string CurrentUserId,
    bool CanEdit,
    bool CanEditAll,
    string? Message,
    IReadOnlyList<string> Errors);

public sealed class ProjectCollaborationInputModel
{
    public int? RecordId { get; set; }

    public string Category { get; set; } = "進度協作";

    public string Content { get; set; } = string.Empty;

    public string? RowVersion { get; set; }
}
