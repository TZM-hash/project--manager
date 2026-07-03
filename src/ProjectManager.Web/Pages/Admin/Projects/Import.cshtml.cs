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

[Authorize(Roles = RoleNames.Administrator)]
public sealed class ImportModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    UserLookupService userLookup,
    AuditLogService auditLogService) : PageModel
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [BindProperty(SupportsGet = true)]
    [Range(2000, 2100)]
    public int ImportYear { get; set; } = DateTime.Today.Year;

    [BindProperty]
    [Required(ErrorMessage = "请选择 Excel 文件。")]
    public IFormFile? UploadFile { get; set; }

    public int CreatedCount { get; private set; }

    public int UpdatedCount { get; private set; }

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
            ModelState.AddModelError(nameof(UploadFile), "请选择有效的 Excel 文件。");
            return Page();
        }

        if (!string.Equals(Path.GetExtension(UploadFile.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(UploadFile), "当前仅支持 .xlsx 文件。");
            return Page();
        }

        var defaultStatusId = await EnsureStatusExistsAsync(cancellationToken);
        if (defaultStatusId is null)
        {
            ModelState.AddModelError(string.Empty, "系统中没有可用项目状态，无法导入。");
            return Page();
        }

        await using var stream = UploadFile.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);
        var rows = worksheet.RowsUsed().Skip(1).ToList();
        if (rows.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Excel 文件中没有数据行。");
            return Page();
        }

        var currentUserId = userManager.GetUserId(User);
        foreach (var row in rows)
        {
            await ImportRowAsync(row, defaultStatusId.Value, currentUserId, cancellationToken);
        }

        return Page();
    }

    public IActionResult OnGetTemplate()
    {
        ImportYear = NormalizeImportYear(ImportYear);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("ProjectsImport");
        var headers = new[] { "工号", "工程名称", "项目人员", "金额", "进度说明" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        sheet.Cell(2, 1).Value = "P-2026-001";
        sheet.Cell(2, 2).Value = "示例工程";
        sheet.Cell(2, 3).Value = "admin";
        sheet.Cell(2, 4).Value = 100000;
        sheet.Cell(2, 5).Value = "已完成资料收集";
        sheet.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        sheet.Range(1, 1, 1, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f6fb");
        sheet.Columns().AdjustToContents();

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return File(
            output.ToArray(),
            ExcelContentType,
            $"项目批量导入模板-{ImportYear}.xlsx");
    }

    private async Task ImportRowAsync(
        IXLRow row,
        int defaultStatusId,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        var projectNumber = row.Cell(1).GetFormattedString().Trim();
        if (string.IsNullOrWhiteSpace(projectNumber))
        {
            return;
        }

        var projectName = row.Cell(2).GetFormattedString().Trim();
        if (string.IsNullOrWhiteSpace(projectName))
        {
            RowErrors.Add($"第 {row.RowNumber()} 行：工程名称不能为空。");
            return;
        }

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
        project.ProjectAmount = ParseDecimal(row.Cell(4).GetFormattedString());
        project.ProgressDescription = RichTextSanitizer.Normalize(row.Cell(5).GetFormattedString());
        project.UpdatedAt = now;
        project.UpdatedByUserId = currentUserId;
        project.CollectionPercent = Math.Clamp(project.CollectionPercent, 0, 100);
        project.ProgressPercent = Math.Clamp(project.ProgressPercent, 0, 100);

        var assignedUserText = row.Cell(3).GetFormattedString();
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
            RowErrors.Add($"第 {row.RowNumber()} 行：项目人员未匹配到系统用户。");
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
            isCreate ? $"批量导入项目 {project.ProjectNumber}" : $"批量更新项目 {project.ProjectNumber}",
            changes,
            cancellationToken);
    }

    private static decimal ParseDecimal(string value)
    {
        return decimal.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static int NormalizeImportYear(int year)
    {
        return year is >= 2000 and <= 2100 ? year : DateTime.Today.Year;
    }
}
