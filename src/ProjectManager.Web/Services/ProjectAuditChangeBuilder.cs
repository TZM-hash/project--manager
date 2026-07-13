using System.Globalization;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public static class ProjectAuditChangeBuilder
{
    /// <summary>
    /// 把 EF 實體壓縮成穩定快照，避免儲存後跟蹤實體變化導致「前後值」混淆。
    /// </summary>
    public static ProjectAuditSnapshot CreateSnapshot(Project project)
    {
        return new ProjectAuditSnapshot(
            project.Id,
            project.Year,
            EmptyToNull(project.ParentCaseNumber),
            project.ProjectNumber,
            project.Name,
            project.Status?.Name ?? project.StatusId.ToString(CultureInfo.InvariantCulture),
            project.ClosedYearMonth?.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            project.ProgressPercent,
            project.ProjectAmount,
            project.CollectionPercent,
            EmptyToNull(project.ProgressDescription),
            project.PurchaseRequests
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Id)
                .Select(CreatePurchaseSnapshot)
                .ToList());
    }

    public static IReadOnlyList<AuditChangeDetail> BuildUpdateChanges(
        ProjectAuditSnapshot before,
        ProjectAuditSnapshot after)
    {
        var changes = new List<AuditChangeDetail>();

        AddIfChanged(changes, "年度", before.Year.ToString(CultureInfo.InvariantCulture), after.Year.ToString(CultureInfo.InvariantCulture));
        AddIfChanged(changes, "母案案號", before.ParentCaseNumber, after.ParentCaseNumber);
        AddIfChanged(changes, "專案工號", before.ProjectNumber, after.ProjectNumber);
        AddIfChanged(changes, "專案名稱", before.Name, after.Name);
        AddIfChanged(changes, "專案狀態", before.StatusName, after.StatusName);
        AddIfChanged(changes, "結案年月", before.ClosedYearMonth, after.ClosedYearMonth);
        AddIfChanged(changes, "專案進度", FormatPercent(before.ProgressPercent), FormatPercent(after.ProgressPercent));
        AddIfChanged(changes, "專案金額", FormatMoney(before.ProjectAmount), FormatMoney(after.ProjectAmount));
        AddIfChanged(changes, "收款比例", FormatPercent(before.CollectionPercent), FormatPercent(after.CollectionPercent));
        AddIfChanged(changes, "進度說明", before.ProgressDescription, after.ProgressDescription);

        // 採購明細透過資料庫 ID 對齊，新增、刪除、修改分別生成不同類型的審計明細。
        var beforePurchases = before.Purchases.ToDictionary(x => x.Id);
        var afterPurchases = after.Purchases.ToDictionary(x => x.Id);

        foreach (var purchase in after.Purchases.Where(x => !beforePurchases.ContainsKey(x.Id)))
        {
            changes.Add(new AuditChangeDetail(
                "PurchaseAdded",
                "新增請購",
                null,
                FormatPurchaseSummary(purchase),
                purchase.RequestNumber));
        }

        foreach (var purchase in before.Purchases.Where(x => !afterPurchases.ContainsKey(x.Id)))
        {
            changes.Add(new AuditChangeDetail(
                "PurchaseDeleted",
                "刪除請購",
                FormatPurchaseSummary(purchase),
                null,
                purchase.RequestNumber));
        }

        foreach (var afterPurchase in after.Purchases.Where(x => beforePurchases.ContainsKey(x.Id)))
        {
            var beforePurchase = beforePurchases[afterPurchase.Id];
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "請購號", beforePurchase.RequestNumber, afterPurchase.RequestNumber);
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "請購類型", beforePurchase.PurchaseTypeName, afterPurchase.PurchaseTypeName);
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "請購人員", beforePurchase.PurchaseStaff, afterPurchase.PurchaseStaff);
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "請購金額", FormatMoney(beforePurchase.PurchaseAmount), FormatMoney(afterPurchase.PurchaseAmount));
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "子案對接人員", beforePurchase.SubCaseContact, afterPurchase.SubCaseContact);
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "付款比例", FormatPercent(beforePurchase.PaymentPercent), FormatPercent(afterPurchase.PaymentPercent));
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "實際已付款", FormatMoney(beforePurchase.ActualPaidAmount), FormatMoney(afterPurchase.ActualPaidAmount));
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "備註", beforePurchase.Notes, afterPurchase.Notes);
        }

        return changes;
    }

    public static IReadOnlyList<AuditChangeDetail> BuildCreateChanges(ProjectAuditSnapshot snapshot)
    {
        var changes = new List<AuditChangeDetail>();

        changes.Add(new AuditChangeDetail("Field", "年度", null, snapshot.Year.ToString(CultureInfo.InvariantCulture), "專案資料"));
        changes.Add(new AuditChangeDetail("Field", "母案案號", null, snapshot.ParentCaseNumber, "專案資料"));
        changes.Add(new AuditChangeDetail("Field", "專案工號", null, snapshot.ProjectNumber, "專案資料"));
        changes.Add(new AuditChangeDetail("Field", "專案名稱", null, snapshot.Name, "專案資料"));
        changes.Add(new AuditChangeDetail("Field", "專案狀態", null, snapshot.StatusName, "專案資料"));
        changes.Add(new AuditChangeDetail("Field", "結案年月", null, snapshot.ClosedYearMonth, "專案資料"));
        changes.Add(new AuditChangeDetail("Field", "專案進度", null, FormatPercent(snapshot.ProgressPercent), "專案資料"));
        changes.Add(new AuditChangeDetail("Field", "專案金額", null, FormatMoney(snapshot.ProjectAmount), "專案資料"));
        changes.Add(new AuditChangeDetail("Field", "收款比例", null, FormatPercent(snapshot.CollectionPercent), "專案資料"));
        changes.Add(new AuditChangeDetail("Field", "進度說明", null, snapshot.ProgressDescription, "專案資料"));

        foreach (var purchase in snapshot.Purchases)
        {
            changes.Add(new AuditChangeDetail(
                "PurchaseAdded",
                "新增請購",
                null,
                FormatPurchaseSummary(purchase),
                purchase.RequestNumber));
        }

        return changes;
    }

    public static IReadOnlyList<AuditChangeDetail> BuildDeleteChanges(ProjectAuditSnapshot snapshot)
    {
        return
        [
            new AuditChangeDetail(
                "ProjectDeleted",
                "刪除專案",
                $"{snapshot.ProjectNumber}，{snapshot.Name}，專案金額 {FormatMoney(snapshot.ProjectAmount)}，專案進度 {FormatPercent(snapshot.ProgressPercent)}",
                null,
                "專案資料")
        ];
    }

    private static PurchaseAuditSnapshot CreatePurchaseSnapshot(PurchaseRequest request)
    {
        return new PurchaseAuditSnapshot(
            request.Id,
            request.RequestNumber,
            request.PurchaseType == PurchaseType.InternalPurchase ? "內購" : "外購",
            DisplayUser(request.PurchaseStaffUserId, request.PurchaseStaff),
            request.PurchaseAmount,
            DisplayUser(request.SubCaseContactUserId, request.SubCaseContact),
            request.PaymentPercent,
            request.ActualPaidAmount,
            EmptyToNull(request.Notes));
    }

    private static void AddIfChanged(
        List<AuditChangeDetail> changes,
        string label,
        string? before,
        string? after)
    {
        // 審計記錄只儲存實際變化的欄位，避免明細頁出現無意義的重複行。
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add(new AuditChangeDetail("Field", label, before, after, "專案資料"));
    }

    private static void AddPurchaseIfChanged(
        List<AuditChangeDetail> changes,
        string scope,
        string label,
        string? before,
        string? after)
    {
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add(new AuditChangeDetail("PurchaseUpdated", label, before, after, scope));
    }

    private static string FormatPurchaseSummary(PurchaseAuditSnapshot purchase)
    {
        return $"{purchase.RequestNumber}，{purchase.PurchaseTypeName}，請購金額 {FormatMoney(purchase.PurchaseAmount)}，付款比例 {FormatPercent(purchase.PaymentPercent)}，實際已付款 {FormatMoney(purchase.ActualPaidAmount)}";
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("N2", CultureInfo.InvariantCulture);
    }

    private static string FormatPercent(decimal value)
    {
        return $"{value.ToString("0.##", CultureInfo.InvariantCulture)}%";
    }

    private static string? DisplayUser(string? userId, ApplicationUser? user)
    {
        if (user is null)
        {
            return EmptyToNull(userId);
        }

        return EmptyToNull(user.DisplayName)
            ?? EmptyToNull(user.UserName)
            ?? EmptyToNull(user.Email)
            ?? EmptyToNull(user.Id);
    }

    private static string? EmptyToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}

public sealed record ProjectAuditSnapshot(
    int Id,
    int Year,
    string? ParentCaseNumber,
    string ProjectNumber,
    string Name,
    string StatusName,
    string? ClosedYearMonth,
    decimal ProgressPercent,
    decimal ProjectAmount,
    decimal CollectionPercent,
    string? ProgressDescription,
    IReadOnlyList<PurchaseAuditSnapshot> Purchases);

public sealed record PurchaseAuditSnapshot(
    int Id,
    string RequestNumber,
    string PurchaseTypeName,
    string? PurchaseStaff,
    decimal PurchaseAmount,
    string? SubCaseContact,
    decimal PaymentPercent,
    decimal ActualPaidAmount,
    string? Notes);
