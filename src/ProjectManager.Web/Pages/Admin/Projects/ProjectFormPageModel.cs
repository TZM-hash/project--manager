using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;
using ProjectType = ProjectManager.Web.Models.ProjectType;

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

    public List<SelectListItem> SubCaseContactOptions { get; private set; } = [];

    public List<SelectListItem> VendorContactOptions { get; private set; } = [];

    public virtual bool IsBasicInfoReadOnly => false;

    public List<SelectListItem> PurchaseTypeOptions { get; } =
    [
        new("內購", ((int)PurchaseType.InternalPurchase).ToString(CultureInfo.InvariantCulture)),
        new("外購", ((int)PurchaseType.ExternalPurchase).ToString(CultureInfo.InvariantCulture))
    ];

    public List<SelectListItem> ProjectTypeOptions { get; } =
    [
        new("保養", ((int)ProjectType.Maintenance).ToString(CultureInfo.InvariantCulture)),
        new("工程", ((int)ProjectType.Engineering).ToString(CultureInfo.InvariantCulture))
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

        // 專案人員排除子案對接人角色
        var subCaseContactUsers = await UserManager.GetUsersInRoleAsync(RoleNames.SubCaseContact);
        var subCaseContactUserIds = subCaseContactUsers.Select(x => x.Id).ToHashSet();

        var users = await UserManager.Users
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        UserOptions = users
            .Where(x => !subCaseContactUserIds.Contains(x.Id))
            .Select(user => new SelectListItem(DisplayUser(user), user.Id))
            .ToList();

        SubCaseContactOptions = users
            .Where(x => subCaseContactUserIds.Contains(x.Id))
            .Select(user => new SelectListItem(DisplayUser(user), user.Id))
            .ToList();

        VendorContactOptions = await Db.VendorContacts
            .AsNoTracking()
            .Include(x => x.Vendor)
            .Where(x => !x.IsDeleted && !x.Vendor!.IsDeleted)
            .OrderBy(x => x.Vendor!.CompanyName)
            .ThenBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Vendor!.CompanyName} - {x.Name}", x.Id.ToString(CultureInfo.InvariantCulture)))
            .ToListAsync(cancellationToken);
    }

    protected void EnsureBlankPurchaseRows(int minimumBlankRows)
    {
        // 表单底部始终保留空白请购行，方便使用者繼續新增多筆请购。
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

        if (!TryParseTrialRunYearMonth(out var trialRunYearMonth))
        {
            trialRunYearMonth = null;
        }

        var status = await Db.ProjectStatuses
            .SingleOrDefaultAsync(x => x.Id == Input.StatusId, cancellationToken);

        if (status is null)
        {
            ModelState.AddModelError("Input.StatusId", "請選擇有效狀態。");
        }

        var project = new Project
        {
            Year = Input.Year,
            ParentCaseNumber = TrimToNull(Input.ParentCaseNumber),
            ProjectNumber = TrimToEmpty(Input.ProjectNumber),
            Name = TrimToEmpty(Input.Name),
            ProjectType = Input.ProjectType,
            StatusId = Input.StatusId,
            ClosedYearMonth = ProjectRules.NormalizeClosedYearMonth(closedYearMonth),
            ProgressPercent = Input.ProgressPercent,
            ProjectAmount = Input.ProjectAmount,
            CollectionPercent = Input.CollectionPercent,
            ProgressDescription = RichTextSanitizer.Normalize(Input.ProgressDescription),
            VendorName = TrimToNull(Input.VendorName),
            TrialRunYearMonth = ProjectRules.NormalizeClosedYearMonth(trialRunYearMonth)
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
            RowVersion = Convert.ToBase64String(project.RowVersion),
            Year = project.Year,
            ParentCaseNumber = project.ParentCaseNumber,
            ProjectNumber = project.ProjectNumber,
            Name = project.Name,
            ProjectType = project.ProjectType,
            StatusId = project.StatusId,
            AssignedUserId = project.Assignments.FirstOrDefault()?.UserId,
            ClosedYearMonth = project.ClosedYearMonth?.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            ProgressPercent = project.ProgressPercent,
            ProjectAmount = project.ProjectAmount,
            CollectionPercent = project.CollectionPercent,
            ProgressDescription = project.ProgressDescription,
            VendorName = project.VendorName,
            TrialRunYearMonth = project.TrialRunYearMonth?.ToString("yyyy-MM", CultureInfo.InvariantCulture),
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
        target.ProjectType = source.ProjectType;
        target.StatusId = source.StatusId;
        target.ClosedYearMonth = source.ClosedYearMonth;
        target.ProgressPercent = source.ProgressPercent;
        target.ProjectAmount = source.ProjectAmount;
        target.CollectionPercent = source.CollectionPercent;
        target.ProgressDescription = source.ProgressDescription;
        target.VendorName = source.VendorName;
        target.TrialRunYearMonth = source.TrialRunYearMonth;
        target.UpdatedByUserId = UserManager.GetUserId(User);
        target.UpdatedAt = now;
    }

    protected void SyncAssignments(Project project)
    {
        // 專案人員改为单选：清空已有，設定新選中的唯一人员
        var existingAssignments = project.Assignments.ToList();
        foreach (var assignment in existingAssignments)
        {
            Db.ProjectAssignments.Remove(assignment);
        }

        if (!string.IsNullOrWhiteSpace(Input.AssignedUserId))
        {
            project.Assignments.Add(new ProjectAssignment
            {
                UserId = Input.AssignedUserId,
                RoleInProject = "專案人員"
            });
        }
    }

    protected void SyncPurchaseRequests(Project project, DateTimeOffset now)
    {
        // 请购子表以隱藏的 Id 对齐已有記錄；新增行没有 Id，刪除行只打软刪除标记。
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

        ModelState.AddModelError("Input.ClosedYearMonth", "結案日期格式不正确。");
        return false;
    }

    private bool TryParseTrialRunYearMonth(out DateOnly? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(Input.TrialRunYearMonth))
        {
            return true;
        }

        if (DateOnly.TryParseExact(
                $"{Input.TrialRunYearMonth}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            value = parsed;
            return true;
        }

        ModelState.AddModelError("Input.TrialRunYearMonth", "預計試車時間格式不正確。");
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
            VendorContactId = request.VendorContactId,
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
            VendorContactId = input.VendorContactId,
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
        target.VendorContactId = source.VendorContactId;
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

    public string RowVersion { get; set; } = string.Empty;

    public int Year { get; set; } = DateTime.Today.Year;

    public string? ParentCaseNumber { get; set; }

    public string? ProjectNumber { get; set; }

    public string? Name { get; set; }

    public ProjectType ProjectType { get; set; } = ProjectType.Engineering;

    public int StatusId { get; set; }

    public string? AssignedUserId { get; set; }

    public string? ClosedYearMonth { get; set; }

    public decimal ProgressPercent { get; set; }

    public decimal ProjectAmount { get; set; }

    public decimal CollectionPercent { get; set; }

    [MaxLength(1000, ErrorMessage = "狀態說明最多允許1000個字符")]
    public string? ProgressDescription { get; set; }

    public string? VendorName { get; set; }

    public string? TrialRunYearMonth { get; set; }

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

    public int? VendorContactId { get; set; }

    public decimal PaymentPercent { get; set; }

    public decimal ActualPaidAmount { get; set; }

    public string? Notes { get; set; }
}
