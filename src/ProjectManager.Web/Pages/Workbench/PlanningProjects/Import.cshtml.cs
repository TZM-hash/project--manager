using System.ComponentModel.DataAnnotations;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Workbench.PlanningProjects;

[Authorize(Roles = RoleNames.BusinessManagerRoles)]
public sealed class ImportModel(
    PlanningProjectService planningProjectService,
    UserLookupService userLookup) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "請選擇 Excel 檔案。")]
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
            ErrorMessage = "請選擇有效的 Excel 檔案。";
            return Page();
        }

        if (!string.Equals(Path.GetExtension(UploadFile.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "当前仅支持 .xlsx 檔案，请下載模板后重新上傳。";
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
                ErrorMessage = "Excel 檔案中没有資料行。";
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

                var leaderUserName = row.Cell(2).GetString().Trim();
                var vendor = row.Cell(3).GetString().Trim();
                var latestDescription = row.Cell(4).GetString().Trim();

                var leaderNames = UserLookupService.SplitNames(leaderUserName).ToArray();
                if (leaderNames.Length > 1)
                {
                    ErrorMessage = $"第 {row.RowNumber()} 行只能指定一位負責人。";
                    return Page();
                }

                string? leaderUserId = null;
                if (leaderNames.Length == 1)
                {
                    leaderUserId = await userLookup.ResolveActiveProjectStaffUserIdAsync(
                        leaderNames[0],
                        cancellationToken);
                    if (string.IsNullOrWhiteSpace(leaderUserId))
                    {
                        ErrorMessage = $"第 {row.RowNumber()} 行的負責人不是有效的一般使用者。";
                        return Page();
                    }
                }

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
                ErrorMessage = "未解析到有效的專案資料。";
                return Page();
            }

            ImportedCount = await planningProjectService.ImportAsync(projects, cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"匯入失敗：{ex.Message}";
        }

        return Page();
    }

    public IActionResult OnGetTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("規劃中專案");
        worksheet.Cell(1, 1).Value = "專案名";
        worksheet.Cell(1, 2).Value = "暫定負責人";
        worksheet.Cell(1, 3).Value = "暫定廠商";
        worksheet.Cell(1, 4).Value = "最新說明";

        worksheet.Cell(2, 1).Value = "示例專案A";
        worksheet.Cell(2, 2).Value = "user1";
        worksheet.Cell(2, 3).Value = "示例廠商";
        worksheet.Cell(2, 4).Value = "这是示例說明";

        worksheet.Column(1).AdjustToContents();
        worksheet.Column(2).AdjustToContents();
        worksheet.Column(3).AdjustToContents();
        worksheet.Column(4).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "規劃中專案匯入模板.xlsx");
    }
}
