using ProjectManager.Web.Models;

namespace ProjectManager.Web.Pages.Shared;

public sealed record StatusTimelineModel(
    IReadOnlyList<ProjectStatus> Statuses,
    int CurrentStatusId,
    IReadOnlySet<int>? SkippedStatusIds = null);
