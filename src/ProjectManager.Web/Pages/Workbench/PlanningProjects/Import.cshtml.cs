using System.ComponentModel.DataAnnotations;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Workbench.PlanningProjects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader)]
public sealed class ImportModel(
    PlanningProjectService planningProjectService,
    UserLookupService userLookup) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "请选择 Excel 文件。")]
    public IFormFile? UploadFile { get; set; }

    public int ImportedCount { get; private set; }

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (UploadFile is null || UploadFile.Length == 0)
        {
            ErrorMessage = "请选择有效的 Excel 文件。";
            return Page();
        }

        if (!string.Equals(Path.GetExtension(UploadFile.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "当前仅支持 .xlsx 文件，请下载模板后重新上传。";
            return Page();
        }

        try
        {
            using var stream = UploadFile.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RowsUsed().Skip(1).ToList();

            if (rows.Count == 0)
            {
                ErrorMessage = "Excel 文件中没有数据行。";
                return Page();
            }

            var projects = new List<PlanningProject>();

            foreach (var row in rows)
            {
                var name = row.Cell(1).GetString().Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var leaderUserNames = row.Cell(2).GetString().Trim();
                var vendor = row.Cell(3).GetString().Trim();
                var latestDescription = row.Cell(4).GetString().Trim();

                var resolvedLeaderIds = await userLookup.ResolveUserIdsAsync(leaderUserNames, cancellationToken);
                var leaderUserId = resolvedLeaderIds.Count > 0
                    ? string.Join(",", resolvedLeaderIds)
                    : null;

                projects.Add(new PlanningProject
                {
                    Name = name,
                    LeaderUserId = leaderUserId,
                    Vendor = string.IsNullOrWhiteSpace(vendor) ? null : vendor,
                    LatestDescription = RichTextSanitizer.Normalize(latestDescription)
                });
            }

            if (projects.Count == 0)
            {
                ErrorMessage = "未解析到有效的项目数据。";
                return Page();
            }

            ImportedCount = await planningProjectService.ImportAsync(projects, cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"导入失败：{ex.Message}";
        }

        return Page();
    }

    public IActionResult OnGetTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("规划中专案");
        worksheet.Cell(1, 1).Value = "项目名";
        worksheet.Cell(1, 2).Value = "项目负责人";
        worksheet.Cell(1, 3).Value = "厂商";
        worksheet.Cell(1, 4).Value = "最新说明";

        worksheet.Cell(2, 1).Value = "示例项目A";
        worksheet.Cell(2, 2).Value = "admin,user1";
        worksheet.Cell(2, 3).Value = "示例厂商";
        worksheet.Cell(2, 4).Value = "这是示例说明";

        worksheet.Column(1).AdjustToContents();
        worksheet.Column(2).AdjustToContents();
        worksheet.Column(3).AdjustToContents();
        worksheet.Column(4).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "规划中专案导入模板.xlsx");
    }
}
