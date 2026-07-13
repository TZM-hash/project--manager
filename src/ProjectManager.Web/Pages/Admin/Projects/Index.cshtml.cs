using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Pages.Shared;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;
using ProjectType = ProjectManager.Web.Models.ProjectType;
using ClosedXML.Excel;
using System.IO;

namespace ProjectManager.Web.Pages.Admin.Projects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader)]
public sealed class IndexModel(
    ProjectQueryService projectQueryService,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    AuditLogService auditLogService,
    ProjectArchiveService projectArchiveService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Year { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ParentCaseNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ProjectNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ProjectName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? PersonnelUserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? StatusId { get; set; }

    [BindProperty(SupportsGet = true)]
    public ProjectType? ProjectType { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool OpenOnly { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 50;

    [BindProperty(SupportsGet = true)]
    public string? ActiveTab { get; set; } = "overview";

    public IReadOnlyList<Project> Projects { get; private set; } = [];

    public IReadOnlyList<Project> EngineeringProjects { get; private set; } = [];

    public IReadOnlyList<Project> PendingClosureProjects { get; private set; } = [];

    public IReadOnlyList<Project> WaitingCollectionProjects { get; private set; } = [];

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public List<SelectListItem> StatusOptions { get; private set; } = [];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public List<SelectListItem> ProjectTypeOptions { get; } =
    [
        new("保養", ((int)Models.ProjectType.Maintenance).ToString()),
        new("工程", ((int)Models.ProjectType.Engineering).ToString())
    ];

    public IReadOnlyList<MetricInsight> Metrics { get; private set; } = [];

    public IReadOnlyList<ChartSlice> StatusSlices { get; private set; } = [];

    public IReadOnlyList<ChartSlice> ProgressSlices { get; private set; } = [];

    public int TotalProjectCount { get; private set; }

    public int EngineeringProjectCount { get; private set; }

    public int MaintenanceProjectCount { get; private set; }

    public decimal TotalProjectAmount { get; private set; }

    public int SelfManagedProjectCount { get; private set; }

    public int ManagedProjectCount { get; private set; }

    public FilterSummaryViewModel FilterSummary => new(
        "./Index",
        BuildFilterSummaryItems(),
        new Dictionary<string, string?> { [nameof(PageSize)] = PageSize.ToString() });

    public PaginationViewModel Pagination => new(
        PageNumber,
        PageSize,
        TotalCount,
        TotalPages,
        "./Index",
        BuildRouteValues());

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadOptionsAsync(cancellationToken);

        if (ActiveTab == "engineering")
        {
            await LoadEngineeringProjectsAsync(cancellationToken);
        }
        else if (ActiveTab == "pending-closure")
        {
            await LoadPendingClosureProjectsAsync(cancellationToken);
        }
        else if (ActiveTab == "waiting-collection")
        {
            await LoadWaitingCollectionProjectsAsync(cancellationToken);
        }
        else
        {
            var page = await projectQueryService.GetProjectsPageAsync(
                new ProjectFilter(Year, ParentCaseNumber, ProjectNumber, ProjectName, PersonnelUserId, StatusId, OpenOnly, null, ProjectType),
                PageNumber,
                PageSize,
                cancellationToken);
            Projects = page.Items;
            TotalCount = page.TotalCount;
            PageNumber = page.PageNumber;
            PageSize = page.PageSize;
            TotalPages = page.TotalPages;
        }

        await LoadInsightsAsync(cancellationToken);
    }

    private async Task LoadEngineeringProjectsAsync(CancellationToken cancellationToken)
    {
        var query = ApplyFilter(db.Projects
            .AsNoTracking()
            .Include(x => x.Status!).ThenInclude(x => x.Style)
            .Include(x => x.Assignments).ThenInclude(x => x.User)
            .Include(x => x.PurchaseRequests).ThenInclude(x => x.VendorContact).ThenInclude(x => x!.Vendor)
            .Include(x => x.GanttPlan).ThenInclude(x => x!.Tasks)
            .Where(x => !x.IsDeleted && x.ProjectType == Models.ProjectType.Engineering));

        EngineeringProjects = await query
            .Where(x => x.Status == null || (!x.Status.IsClosed && x.Status.Code != "PendingClosure"))
            .OrderBy(x => x.ProjectNumber)
            .ToListAsync(cancellationToken);

        SelfManagedProjectCount = EngineeringProjects.Count(x => !x.PurchaseRequests.Any(r => !r.IsDeleted && r.VendorContact != null));
        ManagedProjectCount = EngineeringProjects.Count(x => x.PurchaseRequests.Any(r => !r.IsDeleted && r.VendorContact != null));
    }

    private async Task LoadPendingClosureProjectsAsync(CancellationToken cancellationToken)
    {
        PendingClosureProjects = await ApplySimpleFilter(db.Projects
            .AsNoTracking()
            .Include(x => x.Status!).ThenInclude(x => x.Style)
            .Include(x => x.Assignments).ThenInclude(x => x.User)
            .Where(x => !x.IsDeleted && x.Status != null && x.Status.Code == "PendingClosure"))
            .OrderBy(x => x.ProjectNumber)
            .ToListAsync(cancellationToken);
    }

    private async Task LoadWaitingCollectionProjectsAsync(CancellationToken cancellationToken)
    {
        WaitingCollectionProjects = await ApplySimpleFilter(db.Projects
            .AsNoTracking()
            .Include(x => x.Status!).ThenInclude(x => x.Style)
            .Include(x => x.Assignments).ThenInclude(x => x.User)
            .Where(x => !x.IsDeleted && x.Status != null && x.Status.Code == "WaitingCollection"))
            .OrderBy(x => x.ProjectNumber)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Project> ApplySimpleFilter(IQueryable<Project> query)
    {
        if (Year is not null)
        {
            query = query.Where(x => x.Year == Year);
        }

        if (!string.IsNullOrWhiteSpace(ParentCaseNumber))
        {
            query = query.Where(x => x.ParentCaseNumber != null &&
                                     x.ParentCaseNumber.Contains(ParentCaseNumber));
        }

        if (!string.IsNullOrWhiteSpace(ProjectNumber))
        {
            query = query.Where(x => x.ProjectNumber.Contains(ProjectNumber));
        }

        if (!string.IsNullOrWhiteSpace(ProjectName))
        {
            query = query.Where(x => x.Name.Contains(ProjectName));
        }

        if (!string.IsNullOrWhiteSpace(PersonnelUserId))
        {
            query = query.Where(x => x.Assignments.Any(a => a.UserId == PersonnelUserId));
        }

        if (ProjectType.HasValue)
        {
            query = query.Where(x => x.ProjectType == ProjectType);
        }

        return query;
    }

    public async Task<IActionResult> OnPostBatchDeleteAsync(int[] ids, CancellationToken cancellationToken)
    {
        await DeleteProjectsAsync(ids, cancellationToken);
        return RedirectToPage("./Index", new
        {
            Year,
            ParentCaseNumber,
            ProjectNumber,
            ProjectName,
            PersonnelUserId,
            StatusId,
            ProjectType,
            OpenOnly,
            PageNumber,
            PageSize
        });
    }

    public async Task<IActionResult> OnPostArchiveAsync(int id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        var result = await projectArchiveService.ArchiveProjectAsync(id, userId, cancellationToken);

        if (!result.Success && result.Error is not null)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            TempData["SuccessMessage"] = "專案已成功結案並歸檔";
        }

        return RedirectToPage("./Index", new
        {
            Year,
            ParentCaseNumber,
            ProjectNumber,
            ProjectName,
            PersonnelUserId,
            StatusId,
            ProjectType,
            OpenOnly,
            PageNumber,
            PageSize
        });
    }

    public async Task<IActionResult> OnGetExportExcelAsync(CancellationToken cancellationToken)
    {
        var query = ApplyFilter(db.Projects
            .AsNoTracking()
            .Include(x => x.Status)
            .Include(x => x.Assignments).ThenInclude(x => x.User)
            .Include(x => x.PurchaseRequests).ThenInclude(x => x.VendorContact).ThenInclude(x => x!.Vendor)
            .Where(x => !x.IsDeleted));

        var projects = await query.OrderBy(x => x.ProjectNumber).ToListAsync(cancellationToken);

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var worksheet = workbook.Worksheets.Add("專案清單");

        var headers = new[] { "項次", "工程編號", "專案名稱", "經辦", "專案類型", "狀態", "進度(%)", "進度說明", "金額", "母案案號", "廠商", "結案日期" };
        for (var i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        var headerRow = worksheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Font.FontSize = 11;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#4ECDC4");
        headerRow.Style.Font.FontColor = XLColor.White;
        headerRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRow.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        var row = 2;
        foreach (var project in projects)
        {
            var personnelNames = string.Join("；", project.Assignments.Select(x => x.User?.DisplayName ?? x.User?.UserName ?? x.UserId));
            var vendorName = project.PurchaseRequests
                .FirstOrDefault(r => !r.IsDeleted && r.VendorContact != null)?.VendorContact?.Vendor?.CompanyName ?? "-";

            worksheet.Cell(row, 1).Value = row - 1;
            worksheet.Cell(row, 2).Value = project.ProjectNumber;
            worksheet.Cell(row, 3).Value = project.Name;
            worksheet.Cell(row, 4).Value = personnelNames;
            worksheet.Cell(row, 5).Value = project.ProjectType switch { Models.ProjectType.Engineering => "工程", Models.ProjectType.Maintenance => "保養", _ => "-" };
            worksheet.Cell(row, 6).Value = project.Status?.Name ?? "-";
            worksheet.Cell(row, 7).Value = project.ProgressPercent;
            worksheet.Cell(row, 8).Value = project.ProgressDescription ?? "-";
            worksheet.Cell(row, 9).Value = project.ProjectAmount;
            worksheet.Cell(row, 10).Value = project.ParentCaseNumber ?? "-";
            worksheet.Cell(row, 11).Value = vendorName;
            worksheet.Cell(row, 12).Value = project.ClosedYearMonth?.ToString("yyyy-MM") ?? "-";

            worksheet.Cell(row, 7).Style.NumberFormat.Format = "0";
            worksheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0";

            row++;
        }

        worksheet.Column(1).Width = 8;
        worksheet.Column(2).Width = 15;
        worksheet.Column(3).Width = 30;
        worksheet.Column(4).Width = 20;
        worksheet.Column(5).Width = 12;
        worksheet.Column(6).Width = 12;
        worksheet.Column(7).Width = 12;
        worksheet.Column(8).Width = 25;
        worksheet.Column(9).Width = 15;
        worksheet.Column(10).Width = 15;
        worksheet.Column(11).Width = 20;
        worksheet.Column(12).Width = 15;

        worksheet.Range(2, 1, row - 1, headers.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Range(2, 1, row - 1, headers.Length).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        worksheet.Range(2, 1, row - 1, headers.Length).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        var bytes = ms.ToArray();
        ms.Dispose();

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"專案清單_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    public bool CanArchiveProject(Project project)
    {
        return projectArchiveService.CanArchiveProject(project);
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        StatusOptions = await db.ProjectStatuses
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync(cancellationToken);

        var users = await userManager.Users
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        UserOptions = users
            .Select(x => new SelectListItem(
                string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? x.Id : x.DisplayName,
                x.Id))
            .ToList();

        UserOptions.Insert(0, new SelectListItem("全部", ""));
    }

    private async Task LoadInsightsAsync(CancellationToken cancellationToken)
    {
        var query = ApplyFilter(db.Projects.AsNoTracking().Where(x => !x.IsDeleted));
        var totalAmount = await query.SumAsync(x => (decimal?)x.ProjectAmount, cancellationToken) ?? 0;
        var averageProgress = await query.AverageAsync(x => (decimal?)x.ProgressPercent, cancellationToken) ?? 0;
        var openCount = await query.CountAsync(
            x => x.Status != null && !x.Status.IsClosed,
            cancellationToken);

        TotalProjectCount = await query.CountAsync(cancellationToken);
        EngineeringProjectCount = await query.CountAsync(x => x.ProjectType == Models.ProjectType.Engineering, cancellationToken);
        MaintenanceProjectCount = await query.CountAsync(x => x.ProjectType == Models.ProjectType.Maintenance, cancellationToken);
        TotalProjectAmount = totalAmount;

        Metrics =
        [
            new MetricInsight("篩選結果", TotalCount.ToString("N0"), "目前條件下的專案數"),
            new MetricInsight("未結案", openCount.ToString("N0"), "仍需跟進的專案"),
            new MetricInsight("專案金額", totalAmount.ToString("N2"), "目前篩選彙總", "success"),
            new MetricInsight("平均進度", $"{averageProgress:0.#}%", "目前篩選均值", "info")
        ];

        var statusRows = await query
            .GroupBy(x => x.Status == null ? "未設定" : x.Status.Name)
            .Select(x => new { Label = x.Key, Value = x.Count() })
            .OrderByDescending(x => x.Value)
            .ToListAsync(cancellationToken);
        StatusSlices = ChartPalette.BuildSlices(statusRows.Select(x => (x.Label, (decimal)x.Value)));

        var progressRows = new[]
        {
            ("0-29%", await query.CountAsync(x => x.ProgressPercent < 30, cancellationToken)),
            ("30-69%", await query.CountAsync(x => x.ProgressPercent >= 30 && x.ProgressPercent < 70, cancellationToken)),
            ("70-99%", await query.CountAsync(x => x.ProgressPercent >= 70 && x.ProgressPercent < 100, cancellationToken)),
            ("100%", await query.CountAsync(x => x.ProgressPercent >= 100, cancellationToken))
        };
        ProgressSlices = ChartPalette.BuildSlices(progressRows.Select(x => (x.Item1, (decimal)x.Item2)));
    }

    private IQueryable<Project> ApplyFilter(IQueryable<Project> query)
    {
        if (Year is not null)
        {
            query = query.Where(x => x.Year == Year);
        }

        if (!string.IsNullOrWhiteSpace(ParentCaseNumber))
        {
            query = query.Where(x => x.ParentCaseNumber != null &&
                                     x.ParentCaseNumber.Contains(ParentCaseNumber));
        }

        if (!string.IsNullOrWhiteSpace(ProjectNumber))
        {
            query = query.Where(x => x.ProjectNumber.Contains(ProjectNumber));
        }

        if (!string.IsNullOrWhiteSpace(ProjectName))
        {
            query = query.Where(x => x.Name.Contains(ProjectName));
        }

        if (!string.IsNullOrWhiteSpace(PersonnelUserId))
        {
            query = query.Where(x => x.Assignments.Any(a => a.UserId == PersonnelUserId));
        }

        if (StatusId is not null)
        {
            query = query.Where(x => x.StatusId == StatusId);
        }

        if (ProjectType is not null)
        {
            query = query.Where(x => x.ProjectType == ProjectType);
        }

        if (OpenOnly)
        {
            query = query.Where(x => x.Status != null && !x.Status.IsClosed);
        }

        return query;
    }

    private async Task DeleteProjectsAsync(int[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return;
        }

        var idSet = ids.Distinct().ToArray();
        var projects = await db.Projects
            .Include(x => x.Assignments)
            .Include(x => x.PurchaseRequests)
            .Where(x => !x.IsDeleted && idSet.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var currentUserId = userManager.GetUserId(User);
        var now = DateTimeOffset.UtcNow;

        foreach (var project in projects)
        {
            var before = ProjectAuditChangeBuilder.CreateSnapshot(project);
            project.IsDeleted = true;
            project.UpdatedAt = now;
            project.UpdatedByUserId = currentUserId;

            foreach (var request in project.PurchaseRequests)
            {
                request.IsDeleted = true;
                request.UpdatedAt = now;
            }

            await db.SaveChangesAsync(cancellationToken);
            await auditLogService.LogProjectChangeAsync(
                currentUserId,
                "Delete",
                project.Id,
                project.ProjectNumber,
                $"批量刪除專案 {project.ProjectNumber}",
                ProjectAuditChangeBuilder.BuildDeleteChanges(before),
                cancellationToken);
        }
    }

    private Dictionary<string, string?> BuildRouteValues()
    {
        return new Dictionary<string, string?>
        {
            [nameof(ActiveTab)] = ActiveTab,
            [nameof(Year)] = Year?.ToString(),
            [nameof(ParentCaseNumber)] = ParentCaseNumber,
            [nameof(ProjectNumber)] = ProjectNumber,
            [nameof(ProjectName)] = ProjectName,
            [nameof(PersonnelUserId)] = PersonnelUserId,
            [nameof(StatusId)] = StatusId?.ToString(),
            [nameof(ProjectType)] = ProjectType.HasValue ? ((int)ProjectType.Value).ToString() : null,
            [nameof(OpenOnly)] = OpenOnly.ToString()
        };
    }

    private IReadOnlyList<FilterSummaryItem> BuildFilterSummaryItems()
    {
        var items = new List<FilterSummaryItem>();

        if (Year is not null)
        {
            items.Add(new FilterSummaryItem("年", Year.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(ParentCaseNumber))
        {
            items.Add(new FilterSummaryItem("母案案號", ParentCaseNumber));
        }

        if (!string.IsNullOrWhiteSpace(ProjectNumber))
        {
            items.Add(new FilterSummaryItem("專案工號", ProjectNumber));
        }

        if (!string.IsNullOrWhiteSpace(ProjectName))
        {
            items.Add(new FilterSummaryItem("專案名稱", ProjectName));
        }

        if (!string.IsNullOrWhiteSpace(PersonnelUserId))
        {
            var userText = UserOptions.FirstOrDefault(x => x.Value == PersonnelUserId)?.Text ?? PersonnelUserId;
            items.Add(new FilterSummaryItem("專案人員", userText));
        }

        if (StatusId is not null)
        {
            var statusText = StatusOptions.FirstOrDefault(x => x.Value == StatusId.Value.ToString())?.Text ?? StatusId.Value.ToString();
            items.Add(new FilterSummaryItem("狀態", statusText));
        }

        if (ProjectType is not null)
        {
            var projectTypeText = ProjectTypeOptions.FirstOrDefault(x => x.Value == ((int)ProjectType.Value).ToString())?.Text ?? ProjectType.Value.ToString();
            items.Add(new FilterSummaryItem("專案類型", projectTypeText));
        }

        if (OpenOnly)
        {
            items.Add(new FilterSummaryItem("範圍", "未結案"));
        }

        return items;
    }
}
