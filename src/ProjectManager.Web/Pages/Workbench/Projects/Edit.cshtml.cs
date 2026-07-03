using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Pages.Admin.Projects;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Workbench.Projects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader)]
public sealed class EditModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ProjectMaintenanceService maintenanceService,
    AuditLogService auditLogService)
    : ProjectFormPageModel(db, userManager, maintenanceService)
{
    public override bool IsBasicInfoReadOnly => true;

    [BindProperty]
    public List<int> SkippedStatusIds { get; set; } = [];

    public List<SelectListItem> SkippableStatusOptions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var project = await FindProjectAsync(id, asTracking: false, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        if (!CanEdit(project))
        {
            return Forbid();
        }

        Input = CreateInput(project);
        SkippedStatusIds = project.SkippedStatuses.Select(x => x.StatusId).ToList();
        EnsureBlankPurchaseRows(2);
        await LoadOptionsAsync(cancellationToken);
        await LoadSkippableStatusOptionsAsync(project.StatusId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        var project = await FindProjectAsync(id, asTracking: true, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        if (!CanEdit(project))
        {
            return Forbid();
        }

        LockBasicInfo(project);
        var validation = await ValidateFormAsync(id, cancellationToken);
        if (!validation.IsValid)
        {
            Input.Id = id;
            LockBasicInfo(project);
            EnsureBlankPurchaseRows(2);
            await LoadOptionsAsync(cancellationToken);
            await LoadSkippableStatusOptionsAsync(project.StatusId, cancellationToken);
            return Page();
        }

        var now = DateTimeOffset.UtcNow;
        var before = ProjectAuditChangeBuilder.CreateSnapshot(project);
        var beforeSkipped = await FormatSkippedStatusesAsync(project.SkippedStatuses.Select(x => x.StatusId), cancellationToken);
        ApplyProjectValues(project, validation.Project, now);
        SyncPurchaseRequests(project, now);
        var savedSkippedStatusIds = await SyncSkippedStatusesAsync(project, now, cancellationToken);

        await Db.SaveChangesAsync(cancellationToken);
        var after = ProjectAuditChangeBuilder.CreateSnapshot(project);
        var changes = ProjectAuditChangeBuilder.BuildUpdateChanges(before, after).ToList();
        var afterSkipped = await FormatSkippedStatusesAsync(savedSkippedStatusIds, cancellationToken);
        if (!string.Equals(beforeSkipped, afterSkipped, StringComparison.Ordinal))
        {
            changes.Add(new AuditChangeDetail("Field", "跳过流程节点", beforeSkipped, afterSkipped, "项目资料"));
        }
        if (changes.Count > 0)
        {
            await auditLogService.LogProjectChangeAsync(
                UserManager.GetUserId(User),
                "Update",
                project.Id,
                project.ProjectNumber,
                $"工作台编辑项目 {project.ProjectNumber}",
                changes,
                cancellationToken);
        }

        return RedirectToPage("./Details", new { id });
    }

    private bool CanEdit(Project project)
    {
        if (User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.Leader))
        {
            return true;
        }

        var currentUserId = UserManager.GetUserId(User);
        return !string.IsNullOrWhiteSpace(currentUserId) &&
               project.Assignments.Any(x => x.UserId == currentUserId);
    }

    private void LockBasicInfo(Project project)
    {
        Input.Id = project.Id;
        Input.Year = project.Year;
        Input.ParentCaseNumber = project.ParentCaseNumber;
        Input.ProjectNumber = project.ProjectNumber;
        Input.Name = project.Name;
        Input.AssignedUserId = project.Assignments.FirstOrDefault()?.UserId;
    }

    private async Task<Project?> FindProjectAsync(
        int id,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        var query = Db.Projects
            .Include(x => x.Assignments)
            .Include(x => x.PurchaseRequests)
            .Include(x => x.SkippedStatuses)
            .Where(x => !x.IsDeleted && x.Id == id);

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task LoadSkippableStatusOptionsAsync(int currentStatusId, CancellationToken cancellationToken)
    {
        SkippableStatusOptions = await Db.ProjectStatuses
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsClosed && x.Id != currentStatusId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlySet<int>> SyncSkippedStatusesAsync(
        Project project,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var allowedIds = await Db.ProjectStatuses
            .Where(x => x.IsActive && !x.IsClosed && x.Id != project.StatusId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var selected = SkippedStatusIds
            .Intersect(allowedIds)
            .Distinct()
            .ToHashSet();

        foreach (var skipped in project.SkippedStatuses.ToList())
        {
            if (!selected.Contains(skipped.StatusId))
            {
                Db.ProjectSkippedStatuses.Remove(skipped);
            }
        }

        foreach (var statusId in selected)
        {
            if (project.SkippedStatuses.All(x => x.StatusId != statusId))
            {
                project.SkippedStatuses.Add(new ProjectSkippedStatus
                {
                    StatusId = statusId,
                    CreatedAt = now,
                    CreatedByUserId = UserManager.GetUserId(User)
                });
            }
        }

        return selected;
    }

    private async Task<string> FormatSkippedStatusesAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken)
    {
        var idSet = ids.Distinct().ToArray();
        if (idSet.Length == 0)
        {
            return "-";
        }

        var names = await Db.ProjectStatuses
            .AsNoTracking()
            .Where(x => idSet.Contains(x.Id))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);
        return names.Count == 0 ? "-" : string.Join("、", names);
    }
}
