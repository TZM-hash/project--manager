using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public sealed class ExcelReportService(
    ApplicationDbContext db,
    ProjectQueryService projectQueryService)
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] SettlementHeaders =
    [
        "年",
        "月",
        "批次號",
        "母案案號",
        "專案工號",
        "專案名稱",
        "專案人員",
        "專案進度百分比",
        "專案金額",
        "收款比例",
        "狀態",
        "結案日期",
        "請購號彙總",
        "請購金額合計",
        "子案對接人員彙總",
        "付款比例彙總",
        "實際已付款合計",
        "進度說明",
        "更新人員",
        "來源更新時間"
    ];

    private static readonly string[] OpenProjectHeaders =
    [
        "年",
        "母案案號",
        "專案工號",
        "專案名稱",
        "專案人員",
        "專案進度百分比",
        "專案金額",
        "收款比例",
        "狀態",
        "結案日期",
        "請購金額合計",
        "子案對接人員彙總",
        "實際已付款合計",
        "進度說明",
        "更新人員",
        "最後更新時間"
    ];

    public async Task<ExportFile> ExportSettlementAsync(int batchId, CancellationToken cancellationToken)
    {
        var batch = await db.MonthlySettlementBatches
            .AsNoTracking()
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == batchId, cancellationToken);

        if (batch is null)
        {
            throw new InvalidOperationException($"Settlement batch {batchId} was not found.");
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("月結報表");
        WriteHeaders(sheet, SettlementHeaders);

        var row = 2;
        foreach (var item in batch.Items.OrderBy(x => x.ProjectNumber))
        {
            sheet.Cell(row, 1).SetValue(batch.Year);
            sheet.Cell(row, 2).SetValue(batch.Month);
            sheet.Cell(row, 3).SetValue(batch.BatchNumber);
            sheet.Cell(row, 4).SetValue(item.ParentCaseNumber ?? string.Empty);
            sheet.Cell(row, 5).SetValue(item.ProjectNumber);
            sheet.Cell(row, 6).SetValue(item.ProjectName);
            sheet.Cell(row, 7).SetValue(item.ProjectPersonnelText);
            sheet.Cell(row, 8).SetValue(item.ProgressPercent);
            sheet.Cell(row, 9).SetValue(item.ProjectAmount);
            sheet.Cell(row, 10).SetValue(item.CollectionPercent);
            sheet.Cell(row, 11).SetValue(item.StatusName);
            SetMonthCell(sheet.Cell(row, 12), item.ClosedYearMonth);
            sheet.Cell(row, 13).SetValue(item.PurchaseRequestSummary);
            sheet.Cell(row, 14).SetValue(item.PurchaseAmountTotal);
            sheet.Cell(row, 15).SetValue(item.SubCaseContactSummary);
            sheet.Cell(row, 16).SetValue(item.PaymentPercentSummary);
            sheet.Cell(row, 17).SetValue(item.ActualPaidAmountTotal);
            sheet.Cell(row, 18).SetValue(item.ProgressDescription ?? string.Empty);
            sheet.Cell(row, 19).SetValue(item.UpdatedByUserName);
            sheet.Cell(row, 20).SetValue(item.SourceUpdatedAt.LocalDateTime);
            row++;
        }

        ApplyCommonFormatting(sheet, SettlementHeaders.Length);
        sheet.Column(8).Style.NumberFormat.Format = "0.00";
        sheet.Column(9).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(10).Style.NumberFormat.Format = "0.00";
        sheet.Column(12).Style.DateFormat.Format = "yyyy-mm";
        sheet.Column(14).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(17).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(20).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";

        return ToExportFile(workbook, $"settlement-{batch.Year}-{batch.Month:00}-batch-{batch.BatchNumber}.xlsx");
    }

    public async Task<ExportFile> ExportOpenProjectsAsync(
        ProjectFilter filter,
        CancellationToken cancellationToken)
    {
        var projects = await projectQueryService.GetProjectsAsync(
            filter with { OpenOnly = true },
            cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("未結案專案");
        WriteHeaders(sheet, OpenProjectHeaders);

        var row = 2;
        foreach (var project in projects)
        {
            var purchaseRequests = project.PurchaseRequests.Where(x => !x.IsDeleted).ToList();

            sheet.Cell(row, 1).SetValue(project.Year);
            sheet.Cell(row, 2).SetValue(project.ParentCaseNumber ?? string.Empty);
            sheet.Cell(row, 3).SetValue(project.ProjectNumber);
            sheet.Cell(row, 4).SetValue(project.Name);
            sheet.Cell(row, 5).SetValue(JoinDistinct(project.Assignments.Select(x => DisplayName(x.User))));
            sheet.Cell(row, 6).SetValue(project.ProgressPercent);
            sheet.Cell(row, 7).SetValue(project.ProjectAmount);
            sheet.Cell(row, 8).SetValue(project.CollectionPercent);
            sheet.Cell(row, 9).SetValue(project.Status?.Name ?? string.Empty);
            SetMonthCell(sheet.Cell(row, 10), project.ClosedYearMonth);
            sheet.Cell(row, 11).SetValue(purchaseRequests.Sum(x => x.PurchaseAmount));
            sheet.Cell(row, 12).SetValue(JoinDistinct(purchaseRequests.Select(x => DisplayName(x.SubCaseContact))));
            sheet.Cell(row, 13).SetValue(purchaseRequests.Sum(x => x.ActualPaidAmount));
            sheet.Cell(row, 14).SetValue(project.ProgressDescription ?? string.Empty);
            sheet.Cell(row, 15).SetValue(DisplayName(project.UpdatedByUser));
            sheet.Cell(row, 16).SetValue(project.UpdatedAt.LocalDateTime);
            row++;
        }

        ApplyCommonFormatting(sheet, OpenProjectHeaders.Length);
        sheet.Column(6).Style.NumberFormat.Format = "0.00";
        sheet.Column(7).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(8).Style.NumberFormat.Format = "0.00";
        sheet.Column(10).Style.DateFormat.Format = "yyyy-mm";
        sheet.Column(11).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(13).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(16).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";

        return ToExportFile(workbook, $"open-projects-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    private static void WriteHeaders(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            sheet.Cell(1, i + 1).SetValue(headers[i]);
        }
    }

    private static void ApplyCommonFormatting(IXLWorksheet sheet, int headerCount)
    {
        var headerRange = sheet.Range(1, 1, 1, headerCount);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f4f6");
        sheet.SheetView.FreezeRows(1);
        try
        {
            sheet.Columns(1, headerCount).AdjustToContents();
        }
        catch (UnauthorizedAccessException)
        {
            // Some locked-down Windows deployments cannot enumerate the per-user font folder.
            // Keep exports available with a readable deterministic width in that environment.
            sheet.Columns(1, headerCount).Width = 18;
        }
    }

    private static void SetMonthCell(IXLCell cell, DateOnly? value)
    {
        if (value is null)
        {
            cell.SetValue(string.Empty);
            return;
        }

        cell.SetValue(value.Value.ToDateTime(TimeOnly.MinValue));
    }

    private static ExportFile ToExportFile(XLWorkbook workbook, string fileName)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ExportFile(fileName, ExcelContentType, stream.ToArray());
    }

    private static string DisplayName(ApplicationUser? user)
    {
        if (user is null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(user.DisplayName)
            ? user.UserName ?? string.Empty
            : user.DisplayName;
    }

    private static string JoinDistinct(IEnumerable<string> values)
    {
        return string.Join(
            "; ",
            values
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x));
    }
}

public sealed record ExportFile(
    string FileName,
    string ContentType,
    byte[] Contents);
