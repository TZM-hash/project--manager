using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services.DataViews;

public sealed class DataViewRegistry
{
    private readonly IReadOnlyDictionary<string, DataViewDefinition> definitions;

    public DataViewRegistry()
    {
        definitions = new Dictionary<string, DataViewDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin-projects"] = CreateAdminProjects(),
            ["open-project-report"] = CreateOpenProjectReport(),
            ["workbench-projects"] = CreateWorkbenchProjects(),
            ["maintenance-orders"] = CreateMaintenanceOrders()
        };
    }

    public DataViewDefinition Get(string pageKey)
    {
        if (string.IsNullOrWhiteSpace(pageKey) || !definitions.TryGetValue(pageKey.Trim(), out var definition))
        {
            throw new ArgumentException("不支援的資料檢視頁面。", nameof(pageKey));
        }

        return definition;
    }

    private static DataViewDefinition CreateAdminProjects()
    {
        var columns = ProjectColumns();
        return new(
            "admin-projects",
            new HashSet<string>(["Name", "ProjectNumber", "Year", "ParentCaseNumber", "PersonnelUserId", "StatusId", "ProjectType", "OpenOnly"], StringComparer.OrdinalIgnoreCase),
            columns,
            [
                Preset("daily", "日常管理", columns, ["projectNumber", "name", "assignments", "status", "progress", "collection", "updatedAt", "actions"]),
                Preset("finance", "財務收款", columns, ["projectNumber", "name", "projectAmount", "collection", "status", "updatedAt", "actions"]),
                Preset("purchase", "請購追蹤", columns, ["projectNumber", "name", "assignments", "status", "progress", "actions"]),
                Preset("full", "完整資料", columns, columns.Select(x => x.Key).ToArray())
            ]);
    }

    private static DataViewDefinition CreateOpenProjectReport()
    {
        var columns = new List<DataViewColumnDefinition>
        {
            new("year", "年度", false),
            new("parentCaseNumber", "母案案號", false),
            new("projectNumber", "專案工號", true, true),
            new("name", "專案名稱", true, true),
            new("assignments", "專案人員"),
            new("progress", "進度"),
            new("projectAmount", "專案金額"),
            new("collectionPercent", "收款比例"),
            new("status", "狀態"),
            new("risk", "推進判斷"),
            new("closedYearMonth", "結案日期", false),
            new("purchaseAmount", "請購金額", false),
            new("subCaseContacts", "子案對接人員", false),
            new("actualPaid", "實際已付款", false),
            new("progressDescription", "進度說明", false),
            new("updatedBy", "更新人員", false),
            new("updatedAt", "最後更新時間")
        };
        return new(
            "open-project-report",
            new HashSet<string>(["Name", "ProjectNumber", "Year", "ParentCaseNumber", "PersonnelUserId", "StatusId", "AnalysisType"], StringComparer.OrdinalIgnoreCase),
            columns,
            [
                Preset("risk", "風險關注", columns, ["projectNumber", "name", "assignments", "progress", "collectionPercent", "status", "risk", "projectAmount", "updatedAt"]),
                Preset("progress", "進度管理", columns, ["projectNumber", "name", "assignments", "progress", "status", "risk", "progressDescription", "updatedAt"]),
                Preset("finance", "財務收款", columns, ["projectNumber", "name", "projectAmount", "collectionPercent", "purchaseAmount", "actualPaid", "status"]),
                Preset("full", "完整資料", columns, columns.Select(x => x.Key).ToArray())
            ]);
    }

    private static DataViewDefinition CreateWorkbenchProjects()
    {
        var columns = ProjectColumns();
        return new(
            "workbench-projects",
            new HashSet<string>(["Name", "ProjectNumber", "Year", "ParentCaseNumber", "PersonnelUserId", "StatusId", "ProjectType", "OpenOnly"], StringComparer.OrdinalIgnoreCase),
            columns,
            [
                Preset("my-progress", "我的進度", columns, ["projectNumber", "name", "status", "progress", "collection", "updatedAt", "actions"]),
                Preset("recent-updates", "近期更新", columns, ["projectNumber", "name", "assignments", "status", "updatedAt", "actions"]),
                Preset("full", "完整資料", columns, columns.Select(x => x.Key).ToArray())
            ]);
    }

    private static DataViewDefinition CreateMaintenanceOrders()
    {
        var columns = new List<DataViewColumnDefinition>
        {
            new("item", "項次", true, true),
            new("contractNumber", "合約編號", true, true),
            new("customerName", "客戶名稱", true, true),
            new("siteName", "廠區／據點"),
            new("executor", "執行人員"),
            new("contractStart", "開始日期"),
            new("contractEnd", "結束日期"),
            new("remoteScope", "遠端維保"),
            new("softwareScope", "現場軟體"),
            new("hardwareScope", "現場硬體"),
            new("progress", "進度"),
            new("description", "維保內容說明"),
            new("method", "保養方式"),
            new("onSiteCount", "現場次數"),
            new("remoteCount", "遠端次數"),
            new("handover", "簽收移交"),
            new("updatedAt", "更新日期", false),
            new("updatedBy", "更新人員", false),
            new("actions", "操作", true, true)
        };
        return new(
            "maintenance-orders",
            new HashSet<string>(["Year", "CustomerName", "Method", "ExecutorUserId", "MinProgressPercent", "MaxProgressPercent"], StringComparer.OrdinalIgnoreCase),
            columns,
            [
                Preset("execution", "執行進度", columns, ["item", "contractNumber", "customerName", "progress", "method", "executor", "actions"]),
                Preset("scope", "維保範圍", columns, ["contractNumber", "customerName", "remoteScope", "softwareScope", "hardwareScope", "description", "actions"]),
                Preset("full", "完整資料", columns, columns.Select(x => x.Key).ToArray())
            ]);
    }

    private static List<DataViewColumnDefinition> ProjectColumns()
    {
        var columns = new List<DataViewColumnDefinition>
        {
            new("checkbox", "選取", true, true),
            new("year", "年度"),
            new("parentCaseNumber", "母案案號"),
            new("projectNumber", "專案工號", true, true),
            new("name", "專案名稱", true, true),
            new("assignments", "專案人員"),
            new("projectType", "專案類型"),
            new("status", "狀態"),
            new("progress", "進度"),
            new("collection", "收款比例"),
            new("projectAmount", "專案金額"),
            new("updatedAt", "最後更新時間"),
            new("actions", "操作", true, true)
        };

        return columns;
    }

    private static DataViewPreset Preset(
        string key,
        string name,
        IReadOnlyList<DataViewColumnDefinition> columns,
        IReadOnlyList<string> visibleColumns)
    {
        var validKeys = columns.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new(
            key,
            name,
            new Dictionary<string, string?>(),
            visibleColumns.Where(validKeys.Contains).ToArray());
    }
}
