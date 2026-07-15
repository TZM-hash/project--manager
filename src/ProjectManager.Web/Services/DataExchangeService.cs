using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;

namespace ProjectManager.Web.Services;

public sealed class DataExchangeService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    UserLookupService userLookup)
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<ExportFile> ExportAllAsync(CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        await WriteUsersAsync(workbook, cancellationToken);
        await WriteStatusesAsync(workbook, cancellationToken);
        await WriteProjectsAsync(workbook, cancellationToken);
        await WritePurchaseRequestsAsync(workbook, cancellationToken);
        await WritePlanningProjectsAsync(workbook, cancellationToken);
        await WriteMaintenanceOrdersAsync(workbook, cancellationToken);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return new ExportFile(
            $"project-manager-data-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx",
            ExcelContentType,
            stream.ToArray());
    }

    public async Task<DataImportResult> ImportAllAsync(
        Stream stream,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook(stream);
        var result = new DataImportResult();

        await ImportUsersAsync(workbook, result);
        await ImportStatusesAsync(workbook, result, cancellationToken);
        await ImportProjectsAsync(workbook, result, currentUserId, cancellationToken);
        await ImportPurchaseRequestsAsync(workbook, result, cancellationToken);
        await ImportPlanningProjectsAsync(workbook, result, cancellationToken);
        await ImportMaintenanceOrdersAsync(workbook, result, currentUserId, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task WriteUsersAsync(XLWorkbook workbook, CancellationToken cancellationToken)
    {
        var sheet = workbook.Worksheets.Add("Users");
        WriteHeader(sheet, ["UserName", "DisplayName", "Email", "IsActive", "IsWeakManaged", "Roles", "InitialPassword"]);

        var users = await userManager.Users
            .AsNoTracking()
            .OrderBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        var row = 2;
        foreach (var user in users)
        {
            sheet.Cell(row, 1).SetValue(user.UserName ?? string.Empty);
            sheet.Cell(row, 2).SetValue(user.DisplayName);
            sheet.Cell(row, 3).SetValue(user.Email ?? string.Empty);
            sheet.Cell(row, 4).SetValue(user.IsActive);
            sheet.Cell(row, 5).SetValue(user.IsWeakManaged);
            sheet.Cell(row, 6).SetValue(string.Join(";", await userManager.GetRolesAsync(user)));
            row++;
        }

        ApplySheetStyle(sheet);
    }

    private async Task WriteStatusesAsync(XLWorkbook workbook, CancellationToken cancellationToken)
    {
        var sheet = workbook.Worksheets.Add("ProjectStatuses");
        WriteHeader(sheet, ["Code", "Name", "SortOrder", "IsClosed", "IsActive"]);

        var statuses = await db.ProjectStatuses
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);

        var row = 2;
        foreach (var status in statuses)
        {
            sheet.Cell(row, 1).SetValue(status.Code);
            sheet.Cell(row, 2).SetValue(status.Name);
            sheet.Cell(row, 3).SetValue(status.SortOrder);
            sheet.Cell(row, 4).SetValue(status.IsClosed);
            sheet.Cell(row, 5).SetValue(status.IsActive);
            row++;
        }

        ApplySheetStyle(sheet);
    }

    private async Task WriteProjectsAsync(XLWorkbook workbook, CancellationToken cancellationToken)
    {
        var sheet = workbook.Worksheets.Add("Projects");
        WriteHeader(sheet, [
            "Year", "ParentCaseNumber", "ProjectNumber", "Name", "AssignedUser",
            "StatusCode", "ClosedYearMonth", "ProgressPercent", "ProjectAmount",
            "CollectionPercent", "ProgressDescriptionHtml"
        ]);

        var projects = await db.Projects
            .AsNoTracking()
            .Include(x => x.Assignments)
            .ThenInclude(x => x.User)
            .Include(x => x.Status)
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.Year)
            .ThenBy(x => x.ProjectNumber)
            .ToListAsync(cancellationToken);

        var row = 2;
        foreach (var project in projects)
        {
            sheet.Cell(row, 1).SetValue(project.Year);
            sheet.Cell(row, 2).SetValue(project.ParentCaseNumber ?? string.Empty);
            sheet.Cell(row, 3).SetValue(project.ProjectNumber);
            sheet.Cell(row, 4).SetValue(project.Name);
            sheet.Cell(row, 5).SetValue(DisplayUser(project.Assignments.FirstOrDefault()?.User));
            sheet.Cell(row, 6).SetValue(project.Status?.Code ?? string.Empty);
            sheet.Cell(row, 7).SetValue(project.ClosedYearMonth?.ToString("yyyy-MM") ?? string.Empty);
            sheet.Cell(row, 8).SetValue(project.ProgressPercent);
            sheet.Cell(row, 9).SetValue(project.ProjectAmount);
            sheet.Cell(row, 10).SetValue(project.CollectionPercent);
            sheet.Cell(row, 11).SetValue(RichTextSanitizer.ToPlainText(project.ProgressDescription));
            row++;
        }

        ApplySheetStyle(sheet);
    }

    private async Task WritePurchaseRequestsAsync(XLWorkbook workbook, CancellationToken cancellationToken)
    {
        var sheet = workbook.Worksheets.Add("PurchaseRequests");
        WriteHeader(sheet, [
            "Year", "ProjectNumber", "RequestNumber", "PurchaseType", "PurchaseStaff",
            "PurchaseAmount", "SubCaseContact", "PaymentPercent", "ActualPaidAmount", "Notes"
        ]);

        var requests = await db.PurchaseRequests
            .AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.PurchaseStaff)
            .Include(x => x.SubCaseContact)
            .Where(x => !x.IsDeleted && x.Project != null && !x.Project.IsDeleted)
            .OrderByDescending(x => x.Project!.Year)
            .ThenBy(x => x.Project!.ProjectNumber)
            .ThenBy(x => x.RequestNumber)
            .ToListAsync(cancellationToken);

        var row = 2;
        foreach (var request in requests)
        {
            sheet.Cell(row, 1).SetValue(request.Project?.Year ?? DateTime.Today.Year);
            sheet.Cell(row, 2).SetValue(request.Project?.ProjectNumber ?? string.Empty);
            sheet.Cell(row, 3).SetValue(request.RequestNumber);
            sheet.Cell(row, 4).SetValue(request.PurchaseType == PurchaseType.ExternalPurchase ? "ExternalPurchase" : "InternalPurchase");
            sheet.Cell(row, 5).SetValue(DisplayUser(request.PurchaseStaff));
            sheet.Cell(row, 6).SetValue(request.PurchaseAmount);
            sheet.Cell(row, 7).SetValue(DisplayUser(request.SubCaseContact));
            sheet.Cell(row, 8).SetValue(request.PaymentPercent);
            sheet.Cell(row, 9).SetValue(request.ActualPaidAmount);
            sheet.Cell(row, 10).SetValue(request.Notes ?? string.Empty);
            row++;
        }

        ApplySheetStyle(sheet);
    }

    private async Task WritePlanningProjectsAsync(XLWorkbook workbook, CancellationToken cancellationToken)
    {
        var sheet = workbook.Worksheets.Add("PlanningProjects");
        WriteHeader(sheet, ["Name", "Leaders", "Vendor", "LatestDescription"]);

        var users = await LoadUserLookupAsync(cancellationToken);
        var projects = await db.PlanningProjects
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var row = 2;
        foreach (var project in projects)
        {
            sheet.Cell(row, 1).SetValue(project.Name);
            sheet.Cell(row, 2).SetValue(JoinUserNames(project.LeaderUserId, users));
            sheet.Cell(row, 3).SetValue(project.Vendor ?? string.Empty);
            sheet.Cell(row, 4).SetValue(project.LatestDescription ?? string.Empty);
            row++;
        }

        ApplySheetStyle(sheet);
    }

    private async Task WriteMaintenanceOrdersAsync(XLWorkbook workbook, CancellationToken cancellationToken)
    {
        var sheet = workbook.Worksheets.Add("MaintenanceOrders");
        WriteHeader(sheet, [
            "Year", "CustomerName", "MaintenanceStartDate", "MaintenanceEndDate",
            "MaintenanceMethod", "OnSiteAnnualCount", "RemoteAnnualCount",
            "Executor", "HandoverPercent", "ContractNumber", "SiteName",
            "OnSiteSoftwareFrequency", "OnSiteHardwareFrequency", "ProgressPercent",
            "MaintenanceDescription"
        ]);

        var orders = await db.MaintenanceOrders
            .AsNoTracking()
            .Include(x => x.Executor)
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.Year)
            .ThenBy(x => x.CustomerName)
            .ToListAsync(cancellationToken);

        var row = 2;
        foreach (var order in orders)
        {
            sheet.Cell(row, 1).SetValue(order.Year);
            sheet.Cell(row, 2).SetValue(order.CustomerName);
            sheet.Cell(row, 3).SetValue(order.MaintenanceStartDate.ToString("yyyy-MM-dd"));
            sheet.Cell(row, 4).SetValue(order.MaintenanceEndDate.ToString("yyyy-MM-dd"));
            sheet.Cell(row, 5).SetValue(order.MaintenanceMethod.ToString());
            sheet.Cell(row, 6).SetValue(order.OnSiteAnnualCount);
            sheet.Cell(row, 7).SetValue(order.RemoteAnnualCount);
            sheet.Cell(row, 8).SetValue(DisplayUser(order.Executor));
            sheet.Cell(row, 9).SetValue(order.HandoverPercent);
            sheet.Cell(row, 10).SetValue(order.ContractNumber);
            sheet.Cell(row, 11).SetValue(order.SiteName);
            sheet.Cell(row, 12).SetValue(order.OnSiteSoftwareFrequency);
            sheet.Cell(row, 13).SetValue(order.OnSiteHardwareFrequency);
            sheet.Cell(row, 14).SetValue(order.ProgressPercent);
            sheet.Cell(row, 15).SetValue(order.MaintenanceDescription);
            row++;
        }

        ApplySheetStyle(sheet);
    }

    private async Task ImportUsersAsync(XLWorkbook workbook, DataImportResult result)
    {
        if (!workbook.TryGetWorksheet("Users", out var sheet))
        {
            return;
        }

        foreach (var row in DataRows(sheet))
        {
            var userName = CellText(row, 1);
            if (string.IsNullOrWhiteSpace(userName))
            {
                continue;
            }

            var user = await userManager.FindByNameAsync(userName);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = userName,
                    DisplayName = CellText(row, 2),
                    Email = EmptyToNull(CellText(row, 3)),
                    EmailConfirmed = true,
                    IsActive = CellBool(row, 4, true),
                    IsWeakManaged = CellBool(row, 5, false),
                    CreatedAt = DateTimeOffset.UtcNow
                };

                var password = string.IsNullOrWhiteSpace(CellText(row, 7))
                    ? "ChangeMe123!"
                    : CellText(row, 7);
                var createResult = await userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                {
                    result.AddError("Users", row.RowNumber(), $"使用者 {userName} 建立失敗：{string.Join("；", createResult.Errors.Select(x => x.Description))}");
                    continue;
                }

                result.UsersCreated++;
            }
            else
            {
                user.DisplayName = CellText(row, 2);
                user.Email = EmptyToNull(CellText(row, 3));
                user.IsActive = CellBool(row, 4, true);
                user.IsWeakManaged = CellBool(row, 5, false);
                await userManager.UpdateAsync(user);
                result.UsersUpdated++;
            }

            var roles = UserLookupService.SplitNames(CellText(row, 6))
                .Select(ResolveRoleName)
                .Where(x => x is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var currentRoles = await userManager.GetRolesAsync(user);
            await userManager.RemoveFromRolesAsync(user, currentRoles.Except(roles));
            await userManager.AddToRolesAsync(user, roles.Except(currentRoles));
        }
    }

    private async Task ImportStatusesAsync(
        XLWorkbook workbook,
        DataImportResult result,
        CancellationToken cancellationToken)
    {
        if (!workbook.TryGetWorksheet("ProjectStatuses", out var sheet))
        {
            return;
        }

        foreach (var row in DataRows(sheet))
        {
            var code = CellText(row, 1);
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var status = await db.ProjectStatuses.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
            if (status is null)
            {
                status = new ProjectStatus { Code = code };
                db.ProjectStatuses.Add(status);
                result.StatusesCreated++;
            }
            else
            {
                result.StatusesUpdated++;
            }

            status.Name = RequiredOrDefault(CellText(row, 2), code);
            status.SortOrder = CellInt(row, 3, 0);
            status.IsClosed = CellBool(row, 4, false);
            status.IsActive = CellBool(row, 5, true);
        }
    }

    private async Task ImportProjectsAsync(
        XLWorkbook workbook,
        DataImportResult result,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        if (!workbook.TryGetWorksheet("Projects", out var sheet))
        {
            return;
        }

        foreach (var row in DataRows(sheet))
        {
            var year = CellInt(row, 1, DateTime.Today.Year);
            var projectNumber = CellText(row, 3);
            if (string.IsNullOrWhiteSpace(projectNumber))
            {
                continue;
            }

            var statusId = await ResolveStatusIdAsync(CellText(row, 6), cancellationToken);
            if (statusId is null)
            {
                result.AddError("Projects", row.RowNumber(), "系统中没有可用專案狀態，无法匯入專案。");
                continue;
            }

            var project = await db.Projects
                .Include(x => x.Assignments)
                .SingleOrDefaultAsync(x => !x.IsDeleted && x.Year == year && x.ProjectNumber == projectNumber, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            if (project is null)
            {
                project = new Project
                {
                    Year = year,
                    ProjectNumber = projectNumber,
                    CreatedAt = now
                };
                db.Projects.Add(project);
                result.ProjectsCreated++;
            }
            else
            {
                result.ProjectsUpdated++;
            }

            project.ParentCaseNumber = EmptyToNull(CellText(row, 2));
            project.Name = RequiredOrDefault(CellText(row, 4), projectNumber);
            project.StatusId = statusId.Value;
            project.ClosedYearMonth = ParseMonth(CellText(row, 7));
            project.ProgressPercent = ClampPercent(CellDecimal(row, 8, 0));
            project.ProjectAmount = CellDecimal(row, 9, 0);
            project.CollectionPercent = ClampPercent(CellDecimal(row, 10, 0));
            project.ProgressDescription = RichTextSanitizer.Normalize(CellText(row, 11));
            project.UpdatedAt = now;
            project.UpdatedByUserId = currentUserId;

            var assignedUserId = await ResolveUserIdAsync(CellText(row, 5), cancellationToken);
            db.ProjectAssignments.RemoveRange(project.Assignments);
            if (!string.IsNullOrWhiteSpace(assignedUserId))
            {
                project.Assignments.Add(new ProjectAssignment
                {
                    UserId = assignedUserId,
                    RoleInProject = "ProjectStaff"
                });
            }
        }
    }

    private async Task ImportPurchaseRequestsAsync(
        XLWorkbook workbook,
        DataImportResult result,
        CancellationToken cancellationToken)
    {
        if (!workbook.TryGetWorksheet("PurchaseRequests", out var sheet))
        {
            return;
        }

        foreach (var row in DataRows(sheet))
        {
            var year = CellInt(row, 1, DateTime.Today.Year);
            var projectNumber = CellText(row, 2);
            var requestNumber = CellText(row, 3);
            if (string.IsNullOrWhiteSpace(projectNumber) || string.IsNullOrWhiteSpace(requestNumber))
            {
                continue;
            }

            var project = await db.Projects
                .Include(x => x.PurchaseRequests)
                .SingleOrDefaultAsync(x => !x.IsDeleted && x.Year == year && x.ProjectNumber == projectNumber, cancellationToken);
            if (project is null)
            {
                result.AddError("PurchaseRequests", row.RowNumber(), $"找不到專案 {year}/{projectNumber}。");
                continue;
            }

            var request = project.PurchaseRequests.FirstOrDefault(x => !x.IsDeleted && x.RequestNumber == requestNumber);
            var now = DateTimeOffset.UtcNow;
            if (request is null)
            {
                request = new PurchaseRequest
                {
                    RequestNumber = requestNumber,
                    CreatedAt = now
                };
                project.PurchaseRequests.Add(request);
                result.PurchasesCreated++;
            }
            else
            {
                result.PurchasesUpdated++;
            }

            request.PurchaseType = ParsePurchaseType(CellText(row, 4));
            request.PurchaseStaffUserId = await ResolveUserIdAsync(CellText(row, 5), cancellationToken);
            request.PurchaseAmount = CellDecimal(row, 6, 0);
            request.SubCaseContactUserId = await ResolveUserIdAsync(CellText(row, 7), cancellationToken);
            request.PaymentPercent = ClampPercent(CellDecimal(row, 8, 0));
            request.ActualPaidAmount = CellDecimal(row, 9, 0);
            request.Notes = EmptyToNull(CellText(row, 10));
            request.UpdatedAt = now;
        }
    }

    private async Task ImportPlanningProjectsAsync(
        XLWorkbook workbook,
        DataImportResult result,
        CancellationToken cancellationToken)
    {
        if (!workbook.TryGetWorksheet("PlanningProjects", out var sheet))
        {
            return;
        }

        foreach (var row in DataRows(sheet))
        {
            var name = CellText(row, 1);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var project = await db.PlanningProjects
                .SingleOrDefaultAsync(x => !x.IsDeleted && x.Name == name, cancellationToken);
            if (project is null)
            {
                project = new PlanningProject
                {
                    Name = name,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.PlanningProjects.Add(project);
                result.PlanningCreated++;
            }
            else
            {
                result.PlanningUpdated++;
            }

            var leaderIds = new List<string>();
            foreach (var leader in UserLookupService.SplitNames(CellText(row, 2)))
            {
                var id = await ResolveUserIdAsync(leader, cancellationToken);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    leaderIds.Add(id);
                }
            }

            project.LeaderUserId = leaderIds.Count == 0 ? null : string.Join(",", leaderIds.Distinct());
            project.Vendor = EmptyToNull(CellText(row, 3));
            project.LatestDescription = EmptyToNull(CellText(row, 4));
            project.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task ImportMaintenanceOrdersAsync(
        XLWorkbook workbook,
        DataImportResult result,
        string? currentUserId,
        CancellationToken cancellationToken)
    {
        if (!workbook.TryGetWorksheet("MaintenanceOrders", out var sheet))
        {
            return;
        }

        foreach (var row in DataRows(sheet))
        {
            var year = CellInt(row, 1, DateTime.Today.Year);
            var customerName = CellText(row, 2);
            if (string.IsNullOrWhiteSpace(customerName))
            {
                continue;
            }

            var order = await db.MaintenanceOrders
                .SingleOrDefaultAsync(x => !x.IsDeleted && x.Year == year && x.CustomerName == customerName, cancellationToken);
            if (order is null)
            {
                order = new MaintenanceOrder
                {
                    Year = year,
                    CustomerName = customerName,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.MaintenanceOrders.Add(order);
                result.MaintenanceCreated++;
            }
            else
            {
                result.MaintenanceUpdated++;
            }

            order.MaintenanceStartDate = ParseDate(CellText(row, 3), new DateOnly(year, 1, 1));
            order.MaintenanceEndDate = ParseDate(CellText(row, 4), new DateOnly(year, 12, 31));
            order.MaintenanceMethod = ParseMaintenanceMethod(CellText(row, 5));
            order.OnSiteAnnualCount = CellInt(row, 6, 0);
            order.RemoteAnnualCount = CellInt(row, 7, 0);
            order.ExecutorUserId = await ResolveUserIdAsync(CellText(row, 8), cancellationToken);
            order.HandoverPercent = ClampPercent(CellDecimal(row, 9, 0));
            var generatedContractNumber = $"MO-{year}-{customerName}";
            order.ContractNumber = EmptyToNull(CellText(row, 10))
                ?? generatedContractNumber[..Math.Min(generatedContractNumber.Length, 50)];
            order.SiteName = EmptyToNull(CellText(row, 11)) ?? "主厂区";
            order.OnSiteSoftwareFrequency = EmptyToNull(CellText(row, 12))
                ?? (order.OnSiteAnnualCount > 0 ? "半年/次" : "无");
            order.OnSiteHardwareFrequency = EmptyToNull(CellText(row, 13))
                ?? (order.OnSiteAnnualCount > 0 ? "一年/次" : "无");
            order.ProgressPercent = ClampPercent(CellDecimal(row, 14, order.HandoverPercent));
            order.MaintenanceDescription = EmptyToNull(CellText(row, 15))
                ?? "年度例行保養、远程支持与异常问题处理。";
            order.UpdatedAt = DateTimeOffset.UtcNow;
            order.UpdatedByUserId = currentUserId;
        }
    }

    private async Task<int?> ResolveStatusIdAsync(string value, CancellationToken cancellationToken)
    {
        var status = await db.ProjectStatuses
            .OrderBy(x => x.SortOrder)
            .FirstOrDefaultAsync(
                x => x.Code == value || x.Name == value,
                cancellationToken);

        if (status is not null)
        {
            return status.Id;
        }

        return await db.ProjectStatuses
            .OrderBy(x => x.SortOrder)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string?> ResolveUserIdAsync(string value, CancellationToken cancellationToken)
    {
        return await userLookup.ResolveUserIdAsync(value, cancellationToken);
    }

    private async Task<Dictionary<string, string>> LoadUserLookupAsync(CancellationToken cancellationToken)
    {
        var users = await userManager.Users.AsNoTracking().ToListAsync(cancellationToken);
        return users.ToDictionary(x => x.Id, DisplayUser);
    }

    private static string? ResolveRoleName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return RoleNames.All.FirstOrDefault(x =>
            string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(RoleNames.GetDisplayName(x), trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<IXLRangeRow> DataRows(IXLWorksheet sheet)
    {
        var range = sheet.RangeUsed();
        if (range is null)
        {
            yield break;
        }

        foreach (var row in range.RowsUsed().Skip(1))
        {
            yield return row;
        }
    }

    private static void WriteHeader(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            sheet.Cell(1, i + 1).SetValue(headers[i]);
        }
    }

    private static void ApplySheetStyle(IXLWorksheet sheet)
    {
        var range = sheet.RangeUsed();
        if (range is null)
        {
            return;
        }

        range.Row(1).Style.Font.Bold = true;
        range.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f6fb");
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }

    private static string CellText(IXLRangeRow row, int column)
    {
        return row.Cell(column).GetFormattedString().Trim();
    }

    private static int CellInt(IXLRangeRow row, int column, int fallback)
    {
        return int.TryParse(CellText(row, column), out var value) ? value : fallback;
    }

    private static decimal CellDecimal(IXLRangeRow row, int column, decimal fallback)
    {
        return decimal.TryParse(CellText(row, column), out var value) ? value : fallback;
    }

    private static bool CellBool(IXLRangeRow row, int column, bool fallback)
    {
        var value = CellText(row, column);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("y", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("是", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("啟用", StringComparison.OrdinalIgnoreCase);
    }

    private static DateOnly? ParseMonth(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateOnly.TryParse($"{value}-01", out var parsed)
            ? parsed
            : DateOnly.TryParse(value, out parsed)
                ? new DateOnly(parsed.Year, parsed.Month, 1)
                : null;
    }

    private static DateOnly ParseDate(string value, DateOnly fallback)
    {
        return DateOnly.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static PurchaseType ParsePurchaseType(string value)
    {
        return value.Equals("ExternalPurchase", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("external", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("外購", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("2", StringComparison.OrdinalIgnoreCase)
            ? PurchaseType.ExternalPurchase
            : PurchaseType.InternalPurchase;
    }

    private static MaintenanceMethod ParseMaintenanceMethod(string value)
    {
        if (value.Equals("Remote", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("远程", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("2", StringComparison.OrdinalIgnoreCase))
        {
            return MaintenanceMethod.Remote;
        }

        if (value.Equals("Both", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("现场+远程", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("3", StringComparison.OrdinalIgnoreCase))
        {
            return MaintenanceMethod.Both;
        }

        return MaintenanceMethod.OnSite;
    }

    private static decimal ClampPercent(decimal value)
    {
        return Math.Min(100, Math.Max(0, value));
    }

    private static string DisplayUser(ApplicationUser? user)
    {
        if (user is null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(user.DisplayName)
            ? user.UserName ?? user.Email ?? user.Id
            : user.DisplayName;
    }

    private static string JoinUserNames(string? ids, IReadOnlyDictionary<string, string> users)
    {
        if (string.IsNullOrWhiteSpace(ids))
        {
            return string.Empty;
        }

        return string.Join(
            ";",
            ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(id => users.TryGetValue(id, out var name) ? name : id));
    }

    private static IEnumerable<string> SplitList(string value)
    {
        return value.Split([';', ',', '，', '、'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string RequiredOrDefault(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string? EmptyToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed class DataImportResult
{
    public int TotalProcessed =>
        UsersCreated + UsersUpdated +
        StatusesCreated + StatusesUpdated +
        ProjectsCreated + ProjectsUpdated +
        PurchasesCreated + PurchasesUpdated +
        PlanningCreated + PlanningUpdated +
        MaintenanceCreated + MaintenanceUpdated;

    public int UsersCreated { get; set; }

    public int UsersUpdated { get; set; }

    public int StatusesCreated { get; set; }

    public int StatusesUpdated { get; set; }

    public int ProjectsCreated { get; set; }

    public int ProjectsUpdated { get; set; }

    public int PurchasesCreated { get; set; }

    public int PurchasesUpdated { get; set; }

    public int PlanningCreated { get; set; }

    public int PlanningUpdated { get; set; }

    public int MaintenanceCreated { get; set; }

    public int MaintenanceUpdated { get; set; }

    public List<string> Errors { get; } = [];

    public void AddError(string sheet, int row, string message)
    {
        Errors.Add($"{sheet} 第 {row} 行：{message}");
    }

    public string Summary =>
        $"共成功處理 {TotalProcessed} 筆，錯誤 {Errors.Count} 筆；" +
        $"使用者 新增 {UsersCreated} / 更新 {UsersUpdated}；" +
        $"狀態 新增 {StatusesCreated} / 更新 {StatusesUpdated}；" +
        $"專案 新增 {ProjectsCreated} / 更新 {ProjectsUpdated}；" +
        $"请购 新增 {PurchasesCreated} / 更新 {PurchasesUpdated}；" +
        $"規劃中專案 新增 {PlanningCreated} / 更新 {PlanningUpdated}；" +
        $"保養訂單 新增 {MaintenanceCreated} / 更新 {MaintenanceUpdated}";
}
