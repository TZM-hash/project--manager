using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Shared;

public sealed record ProjectGanttPanelModel(
    Project Project,
    ProjectGanttInputModel Input,
    bool CanEdit,
    string? Message,
    IReadOnlyList<string> Errors);
