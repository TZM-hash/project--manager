using System.Globalization;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public static class ProjectAuditChangeBuilder
{
    /// <summary>
    /// 把 EF 实体压缩成稳定快照，避免保存后跟踪实体变化导致“前后值”混淆。
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
        AddIfChanged(changes, "母案案号", before.ParentCaseNumber, after.ParentCaseNumber);
        AddIfChanged(changes, "项目工号", before.ProjectNumber, after.ProjectNumber);
        AddIfChanged(changes, "项目名称", before.Name, after.Name);
        AddIfChanged(changes, "项目状态", before.StatusName, after.StatusName);
        AddIfChanged(changes, "结案年月", before.ClosedYearMonth, after.ClosedYearMonth);
        AddIfChanged(changes, "项目进度", FormatPercent(before.ProgressPercent), FormatPercent(after.ProgressPercent));
        AddIfChanged(changes, "项目金额", FormatMoney(before.ProjectAmount), FormatMoney(after.ProjectAmount));
        AddIfChanged(changes, "收款比例", FormatPercent(before.CollectionPercent), FormatPercent(after.CollectionPercent));
        AddIfChanged(changes, "进度说明", before.ProgressDescription, after.ProgressDescription);

        // 采购明细通过数据库 ID 对齐，新增、删除、修改分别生成不同类型的审计明细。
        var beforePurchases = before.Purchases.ToDictionary(x => x.Id);
        var afterPurchases = after.Purchases.ToDictionary(x => x.Id);

        foreach (var purchase in after.Purchases.Where(x => !beforePurchases.ContainsKey(x.Id)))
        {
            changes.Add(new AuditChangeDetail(
                "PurchaseAdded",
                "新增请购",
                null,
                FormatPurchaseSummary(purchase),
                purchase.RequestNumber));
        }

        foreach (var purchase in before.Purchases.Where(x => !afterPurchases.ContainsKey(x.Id)))
        {
            changes.Add(new AuditChangeDetail(
                "PurchaseDeleted",
                "删除请购",
                FormatPurchaseSummary(purchase),
                null,
                purchase.RequestNumber));
        }

        foreach (var afterPurchase in after.Purchases.Where(x => beforePurchases.ContainsKey(x.Id)))
        {
            var beforePurchase = beforePurchases[afterPurchase.Id];
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "请购号", beforePurchase.RequestNumber, afterPurchase.RequestNumber);
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "请购类型", beforePurchase.PurchaseTypeName, afterPurchase.PurchaseTypeName);
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "请购人员", beforePurchase.PurchaseStaff, afterPurchase.PurchaseStaff);
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "请购金额", FormatMoney(beforePurchase.PurchaseAmount), FormatMoney(afterPurchase.PurchaseAmount));
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "子案对接人员", beforePurchase.SubCaseContact, afterPurchase.SubCaseContact);
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "付款比例", FormatPercent(beforePurchase.PaymentPercent), FormatPercent(afterPurchase.PaymentPercent));
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "实际已付款", FormatMoney(beforePurchase.ActualPaidAmount), FormatMoney(afterPurchase.ActualPaidAmount));
            AddPurchaseIfChanged(changes, beforePurchase.RequestNumber, "备注", beforePurchase.Notes, afterPurchase.Notes);
        }

        return changes;
    }

    public static IReadOnlyList<AuditChangeDetail> BuildCreateChanges(ProjectAuditSnapshot snapshot)
    {
        var changes = new List<AuditChangeDetail>();

        changes.Add(new AuditChangeDetail("Field", "年度", null, snapshot.Year.ToString(CultureInfo.InvariantCulture), "项目资料"));
        changes.Add(new AuditChangeDetail("Field", "母案案号", null, snapshot.ParentCaseNumber, "项目资料"));
        changes.Add(new AuditChangeDetail("Field", "项目工号", null, snapshot.ProjectNumber, "项目资料"));
        changes.Add(new AuditChangeDetail("Field", "项目名称", null, snapshot.Name, "项目资料"));
        changes.Add(new AuditChangeDetail("Field", "项目状态", null, snapshot.StatusName, "项目资料"));
        changes.Add(new AuditChangeDetail("Field", "结案年月", null, snapshot.ClosedYearMonth, "项目资料"));
        changes.Add(new AuditChangeDetail("Field", "项目进度", null, FormatPercent(snapshot.ProgressPercent), "项目资料"));
        changes.Add(new AuditChangeDetail("Field", "项目金额", null, FormatMoney(snapshot.ProjectAmount), "项目资料"));
        changes.Add(new AuditChangeDetail("Field", "收款比例", null, FormatPercent(snapshot.CollectionPercent), "项目资料"));
        changes.Add(new AuditChangeDetail("Field", "进度说明", null, snapshot.ProgressDescription, "项目资料"));

        foreach (var purchase in snapshot.Purchases)
        {
            changes.Add(new AuditChangeDetail(
                "PurchaseAdded",
                "新增请购",
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
                "删除项目",
                $"{snapshot.ProjectNumber}，{snapshot.Name}，项目金额 {FormatMoney(snapshot.ProjectAmount)}，项目进度 {FormatPercent(snapshot.ProgressPercent)}",
                null,
                "项目资料")
        ];
    }

    private static PurchaseAuditSnapshot CreatePurchaseSnapshot(PurchaseRequest request)
    {
        return new PurchaseAuditSnapshot(
            request.Id,
            request.RequestNumber,
            request.PurchaseType == PurchaseType.InternalPurchase ? "内购" : "外购",
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
        // 审计记录只保存实际变化的字段，避免详情页出现无意义的重复行。
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add(new AuditChangeDetail("Field", label, before, after, "项目资料"));
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
        return $"{purchase.RequestNumber}，{purchase.PurchaseTypeName}，请购金额 {FormatMoney(purchase.PurchaseAmount)}，付款比例 {FormatPercent(purchase.PaymentPercent)}，实际已付款 {FormatMoney(purchase.ActualPaidAmount)}";
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
