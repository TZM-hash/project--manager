using System.ComponentModel.DataAnnotations;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.Projects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader)]
public sealed class ImportModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    UserLookupService userLookup,
    AuditLogService auditLogService,
    OpenCcConverterService openCcConverter) : PageModel
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [BindProperty(SupportsGet = true)]
    [Range(2000, 2100)]
    public int ImportYear { get; set; } = DateTime.Today.Year;

    [BindProperty]
    [Required(ErrorMessage = "請選擇 Excel 檔案。")]
    public IFormFile? UploadFile { get; set; }

    public int CreatedCount { get; private set; }

    public int UpdatedCount { get; private set; }

    public int TotalRowCount { get; private set; }

    public int SkippedCount => Math.Max(0, TotalRowCount - CreatedCount - UpdatedCount);

    public List<string> RowErrors { get; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ImportYear = NormalizeImportYear(ImportYear);
        await EnsureStatusExistsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (UploadFile is null || UploadFile.Length == 0)
        {
            ModelState.AddModelError(nameof(UploadFile), "請選擇有效的 Excel 檔案。");
            return Page();
        }

        if (!string.Equals(Path.GetExtension(UploadFile.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(UploadFile), "当前仅支持 .xlsx 檔案。");
            return Page();
        }

        var defaultStatusId = await EnsureStatusExistsAsync(cancellationToken);
        if (defaultStatusId is null)
        {
            ModelState.AddModelError(string.Empty, "系统中没有可用專案狀態，无法匯入。");
            return Page();
        }

        var allStatuses = await db.ProjectStatuses
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var statusMap = BuildStatusMap(allStatuses);

        await using var stream = UploadFile.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);
        var rows = worksheet.RowsUsed().Skip(1).ToList();
        TotalRowCount = rows.Count;
        if (rows.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Excel 檔案中没有資料行。");
            return Page();
        }

        var currentUserId = userManager.GetUserId(User);
        foreach (var row in rows)
        {
            await ImportRowAsync(row, defaultStatusId.Value, statusMap, currentUserId, cancellationToken);
        }

        return Page();
    }

    public IActionResult OnGetTemplate()
    {
        ImportYear = NormalizeImportYear(ImportYear);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("ProjectsImport");
        var headers = new[] { "項次", "工程編號", "工程名稱", "經辦", "專案類型", "狀態說明", "受訂金額（含稅）" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        sheet.Cell(2, 1).Value = 1;
        sheet.Cell(2, 2).Value = "P-2026-001";
        sheet.Cell(2, 3).Value = "示例工程";
        sheet.Cell(2, 4).Value = "admin";
        sheet.Cell(2, 5).Value = "工程";
        sheet.Cell(2, 6).Value = "進行中";
        sheet.Cell(2, 7).Value = 100000;
        sheet.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        sheet.Range(1, 1, 1, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f6fb");
        sheet.Columns().AdjustToContents();

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return File(
            output.ToArray(),
            ExcelContentType,
            $"專案批量匯入模板-{ImportYear}.xlsx");
    }

    private async Task ImportRowAsync(
        IXLRow row,
        int defaultStatusId,
        Dictionary<string, int> statusMap,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        var projectNumber = row.Cell(2).GetFormattedString().Trim();
        if (string.IsNullOrWhiteSpace(projectNumber))
        {
            return;
        }

        var projectName = row.Cell(3).GetFormattedString().Trim();
        if (string.IsNullOrWhiteSpace(projectName))
        {
            RowErrors.Add($"第 {row.RowNumber()} 行：工程名稱不能为空。");
            return;
        }

        var projectTypeText = row.Cell(5).GetFormattedString().Trim();
        var projectType = ParseProjectType(projectTypeText);

        var project = await db.Projects
            .Include(x => x.Assignments)
            .Include(x => x.PurchaseRequests)
            .SingleOrDefaultAsync(
                x => !x.IsDeleted &&
                     x.Year == ImportYear &&
                     x.ProjectNumber == projectNumber,
                cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var isCreate = project is null;
        project ??= new Project
        {
            Year = ImportYear,
            ProjectNumber = projectNumber,
            StatusId = defaultStatusId,
            CreatedAt = now
        };

        var before = isCreate ? null : ProjectAuditChangeBuilder.CreateSnapshot(project);
        project.Name = projectName;
        project.ProjectType = projectType;
        project.ProjectAmount = ParseDecimal(row.Cell(7).GetFormattedString());

        var statusText = row.Cell(6).GetFormattedString().Trim();
        if (!string.IsNullOrWhiteSpace(statusText) && statusMap.TryGetValue(statusText, out var statusId))
        {
            project.StatusId = statusId;
        }
        else if (!string.IsNullOrWhiteSpace(statusText))
        {
            RowErrors.Add($"第 {row.RowNumber()} 行：狀態說明「{statusText}」未匹配到系统狀態，使用預設狀態。");
        }

        project.UpdatedAt = now;
        project.UpdatedByUserId = currentUserId;
        project.CollectionPercent = Math.Clamp(project.CollectionPercent, 0, 100);
        project.ProgressPercent = Math.Clamp(project.ProgressPercent, 0, 100);

        var assignedUserText = row.Cell(4).GetFormattedString();
        var assignedUserIds = await userLookup.ResolveUserIdsAsync(assignedUserText, cancellationToken);
        var existingAssignments = project.Assignments.ToList();
        db.ProjectAssignments.RemoveRange(existingAssignments);
        project.Assignments.Clear();
        foreach (var assignedUserId in assignedUserIds)
        {
            project.Assignments.Add(new ProjectAssignment
            {
                UserId = assignedUserId,
                RoleInProject = "ProjectStaff"
            });
        }
        if (!string.IsNullOrWhiteSpace(assignedUserText) && assignedUserIds.Count == 0)
        {
            RowErrors.Add($"第 {row.RowNumber()} 行：專案人員未匹配到系统使用者。");
        }

        if (isCreate)
        {
            db.Projects.Add(project);
            CreatedCount++;
        }
        else
        {
            UpdatedCount++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(project, before, isCreate, currentUserId, cancellationToken);
    }

    private async Task<int?> EnsureStatusExistsAsync(CancellationToken cancellationToken)
    {
        return await db.ProjectStatuses
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task WriteAuditAsync(
        Project project,
        ProjectAuditSnapshot? before,
        bool isCreate,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        var after = ProjectAuditChangeBuilder.CreateSnapshot(project);
        var changes = isCreate || before is null
            ? ProjectAuditChangeBuilder.BuildCreateChanges(after)
            : ProjectAuditChangeBuilder.BuildUpdateChanges(before, after);
        if (changes.Count == 0)
        {
            return;
        }

        await auditLogService.LogProjectChangeAsync(
            currentUserId,
            isCreate ? "Create" : "Update",
            project.Id,
            project.ProjectNumber,
            isCreate ? $"批量匯入專案 {project.ProjectNumber}" : $"批量更新專案 {project.ProjectNumber}",
            changes,
            cancellationToken);
    }

    private static ProjectType ParseProjectType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ProjectType.Engineering;
        }

        var normalized = value.Trim();
        if (normalized.Contains("保養") || normalized.Contains("保养") ||
            string.Equals(normalized, "Maintenance", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectType.Maintenance;
        }

        if (normalized.Contains("工程") ||
            string.Equals(normalized, "Engineering", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectType.Engineering;
        }

        return ProjectType.Engineering;
    }

    private static decimal ParseDecimal(string value)
    {
        return decimal.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static int NormalizeImportYear(int year)
    {
        return year is >= 2000 and <= 2100 ? year : DateTime.Today.Year;
    }

    private Dictionary<string, int> BuildStatusMap(List<ProjectStatus> statuses)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var status in statuses)
        {
            var name = status.Name?.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                map[name] = status.Id;
                var traditionalName = openCcConverter.ToTraditional(name);
                if (!string.Equals(traditionalName, name, StringComparison.OrdinalIgnoreCase))
                {
                    map[traditionalName] = status.Id;
                }
                var simplifiedName = openCcConverter.ToSimplified(name);
                if (!string.Equals(simplifiedName, name, StringComparison.OrdinalIgnoreCase))
                {
                    map[simplifiedName] = status.Id;
                }
            }

            var code = status.Code?.Trim();
            if (!string.IsNullOrWhiteSpace(code))
            {
                map[code] = status.Id;
            }
        }
        return map;
    }
}
