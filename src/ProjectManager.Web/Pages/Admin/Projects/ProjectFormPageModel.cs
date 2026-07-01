using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.Projects;

public abstract class ProjectFormPageModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ProjectMaintenanceService maintenanceService) : PageModel
{
    protected ApplicationDbContext Db { get; } = db;

    protected UserManager<ApplicationUser> UserManager { get; } = userManager;

    [BindProperty]
    public ProjectInputModel Input { get; set; } = new();

    public List<SelectListItem> StatusOptions { get; private set; } = [];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public List<SelectListItem> PurchaseTypeOptions { get; } =
    [
        new("内购", ((int)PurchaseType.InternalPurchase).ToString(CultureInfo.InvariantCulture)),
        new("外购", ((int)PurchaseType.ExternalPurchase).ToString(CultureInfo.InvariantCulture))
    ];

    protected async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        var statuses = await Db.ProjectStatuses
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        StatusOptions = statuses
            .Select(status => new SelectListItem
            {
                Text = status.IsActive ? status.Name : $"{status.Name}（停用）",
                Value = status.Id.ToString(CultureInfo.InvariantCulture),
                Disabled = !status.IsActive && status.Id != Input.StatusId
            })
            .ToList();

        var users = await UserManager.Users
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        UserOptions = users
            .Select(user => new SelectListItem(DisplayUser(user), user.Id))
            .ToList();
    }

    protected void EnsureBlankPurchaseRows(int minimumBlankRows)
    {
        var blankRows = Input.Purchases.Count(x => x.Id == 0 && !HasPurchaseData(x));
        while (blankRows < minimumBlankRows)
        {
            Input.Purchases.Add(new PurchaseInputModel());
            blankRows++;
        }
    }

    protected async Task<FormValidationResult> ValidateFormAsync(
        int? existingProjectId,
        CancellationToken cancellationToken)
    {
        if (!TryParseClosedYearMonth(out var closedYearMonth))
        {
            closedYearMonth = null;
        }

        var status = await Db.ProjectStatuses
            .SingleOrDefaultAsync(x => x.Id == Input.StatusId, cancellationToken);

        if (status is null)
        {
            ModelState.AddModelError("Input.StatusId", "请选择有效状态。");
        }

        var project = new Project
        {
            Year = Input.Year,
            ParentCaseNumber = TrimToNull(Input.ParentCaseNumber),
            ProjectNumber = TrimToEmpty(Input.ProjectNumber),
            Name = TrimToEmpty(Input.Name),
            StatusId = Input.StatusId,
            ClosedYearMonth = ProjectRules.NormalizeClosedYearMonth(closedYearMonth),
            ProgressPercent = Input.ProgressPercent,
            ProjectAmount = Input.ProjectAmount,
            CollectionPercent = Input.CollectionPercent,
            ProgressDescription = TrimToNull(Input.ProgressDescription)
        };

        var purchaseRequests = Input.Purchases
            .Where(x => !x.IsDeleted && HasPurchaseData(x))
            .Select(CreatePurchaseRequest)
            .ToList();

        var errors = await maintenanceService.ValidateForSaveAsync(
            project,
            purchaseRequests,
            existingProjectId,
            status?.IsClosed ?? false,
            cancellationToken);

        foreach (var error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        return new FormValidationResult(ModelState.IsValid, project, purchaseRequests);
    }

    protected static ProjectInputModel CreateInput(Project project)
    {
        return new ProjectInputModel
        {
            Id = project.Id,
            Year = project.Year,
            ParentCaseNumber = project.ParentCaseNumber,
            ProjectNumber = project.ProjectNumber,
            Name = project.Name,
            StatusId = project.StatusId,
            AssignedUserIds = project.Assignments.Select(x => x.UserId).ToList(),
            ClosedYearMonth = project.ClosedYearMonth?.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            ProgressPercent = project.ProgressPercent,
            ProjectAmount = project.ProjectAmount,
            CollectionPercent = project.CollectionPercent,
            ProgressDescription = project.ProgressDescription,
            Purchases = project.PurchaseRequests
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Id)
                .Select(CreatePurchaseInput)
                .ToList()
        };
    }

    protected void ApplyProjectValues(Project target, Project source, DateTimeOffset now)
    {
        target.Year = source.Year;
        target.ParentCaseNumber = source.ParentCaseNumber;
        target.ProjectNumber = source.ProjectNumber;
        target.Name = source.Name;
        target.StatusId = source.StatusId;
        target.ClosedYearMonth = source.ClosedYearMonth;
        target.ProgressPercent = source.ProgressPercent;
        target.ProjectAmount = source.ProjectAmount;
        target.CollectionPercent = source.CollectionPercent;
        target.ProgressDescription = source.ProgressDescription;
        target.UpdatedByUserId = UserManager.GetUserId(User);
        target.UpdatedAt = now;
    }

    protected void SyncAssignments(Project project)
    {
        var selectedUserIds = Input.AssignedUserIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var assignment in project.Assignments
                     .Where(x => !selectedUserIds.Contains(x.UserId))
                     .ToList())
        {
            Db.ProjectAssignments.Remove(assignment);
        }

        var existingUserIds = project.Assignments
            .Select(x => x.UserId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var userId in selectedUserIds.Where(x => !existingUserIds.Contains(x)))
        {
            project.Assignments.Add(new ProjectAssignment
            {
                UserId = userId,
                RoleInProject = "专案人员"
            });
        }
    }

    protected void SyncPurchaseRequests(Project project, DateTimeOffset now)
    {
        var existingById = project.PurchaseRequests.ToDictionary(x => x.Id);

        foreach (var input in Input.Purchases)
        {
            if (input.Id > 0 && existingById.TryGetValue(input.Id, out var existing))
            {
                if (input.IsDeleted)
                {
                    existing.IsDeleted = true;
                    existing.UpdatedAt = now;
                    continue;
                }

                ApplyPurchaseValues(existing, input, now);
                existing.IsDeleted = false;
            }
            else if (!input.IsDeleted && HasPurchaseData(input))
            {
                var request = CreatePurchaseRequest(input);
                request.CreatedAt = now;
                request.UpdatedAt = now;
                project.PurchaseRequests.Add(request);
            }
        }
    }

    protected static bool HasPurchaseData(PurchaseInputModel input)
    {
        return input.Id > 0 ||
               !string.IsNullOrWhiteSpace(input.RequestNumber) ||
               !string.IsNullOrWhiteSpace(input.PurchaseStaffUserId) ||
               !string.IsNullOrWhiteSpace(input.SubCaseContactUserId) ||
               !string.IsNullOrWhiteSpace(input.Notes) ||
               input.PurchaseAmount != 0 ||
               input.PaymentPercent != 0 ||
               input.ActualPaidAmount != 0;
    }

    private bool TryParseClosedYearMonth(out DateOnly? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(Input.ClosedYearMonth))
        {
            return true;
        }

        if (DateOnly.TryParseExact(
                $"{Input.ClosedYearMonth}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            value = parsed;
            return true;
        }

        ModelState.AddModelError("Input.ClosedYearMonth", "结案日期格式不正确。");
        return false;
    }

    private static PurchaseInputModel CreatePurchaseInput(PurchaseRequest request)
    {
        return new PurchaseInputModel
        {
            Id = request.Id,
            RequestNumber = request.RequestNumber,
            PurchaseType = request.PurchaseType,
            PurchaseStaffUserId = request.PurchaseStaffUserId,
            PurchaseAmount = request.PurchaseAmount,
            SubCaseContactUserId = request.SubCaseContactUserId,
            PaymentPercent = request.PaymentPercent,
            ActualPaidAmount = request.ActualPaidAmount,
            Notes = request.Notes
        };
    }

    private static PurchaseRequest CreatePurchaseRequest(PurchaseInputModel input)
    {
        return new PurchaseRequest
        {
            RequestNumber = TrimToEmpty(input.RequestNumber),
            PurchaseType = input.PurchaseType,
            PurchaseStaffUserId = TrimToNull(input.PurchaseStaffUserId),
            PurchaseAmount = input.PurchaseAmount,
            SubCaseContactUserId = TrimToNull(input.SubCaseContactUserId),
            PaymentPercent = input.PaymentPercent,
            ActualPaidAmount = input.ActualPaidAmount,
            Notes = TrimToNull(input.Notes)
        };
    }

    private static void ApplyPurchaseValues(
        PurchaseRequest target,
        PurchaseInputModel source,
        DateTimeOffset now)
    {
        target.RequestNumber = TrimToEmpty(source.RequestNumber);
        target.PurchaseType = source.PurchaseType;
        target.PurchaseStaffUserId = TrimToNull(source.PurchaseStaffUserId);
        target.PurchaseAmount = source.PurchaseAmount;
        target.SubCaseContactUserId = TrimToNull(source.SubCaseContactUserId);
        target.PaymentPercent = source.PaymentPercent;
        target.ActualPaidAmount = source.ActualPaidAmount;
        target.Notes = TrimToNull(source.Notes);
        target.UpdatedAt = now;
    }

    private static string DisplayUser(ApplicationUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName;
        }

        return user.UserName ?? user.Email ?? user.Id;
    }

    private static string TrimToEmpty(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}

public sealed record FormValidationResult(
    bool IsValid,
    Project Project,
    IReadOnlyList<PurchaseRequest> PurchaseRequests);

public sealed class ProjectInputModel
{
    public int Id { get; set; }

    public int Year { get; set; } = DateTime.Today.Year;

    public string? ParentCaseNumber { get; set; }

    public string? ProjectNumber { get; set; }

    public string? Name { get; set; }

    public int StatusId { get; set; }

    public List<string> AssignedUserIds { get; set; } = [];

    public string? ClosedYearMonth { get; set; }

    public decimal ProgressPercent { get; set; }

    public decimal ProjectAmount { get; set; }

    public decimal CollectionPercent { get; set; }

    public string? ProgressDescription { get; set; }

    public List<PurchaseInputModel> Purchases { get; set; } = [];
}

public sealed class PurchaseInputModel
{
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    public string? RequestNumber { get; set; }

    public PurchaseType PurchaseType { get; set; } = PurchaseType.InternalPurchase;

    public string? PurchaseStaffUserId { get; set; }

    public decimal PurchaseAmount { get; set; }

    public string? SubCaseContactUserId { get; set; }

    public decimal PaymentPercent { get; set; }

    public decimal ActualPaidAmount { get; set; }

    public string? Notes { get; set; }
}
