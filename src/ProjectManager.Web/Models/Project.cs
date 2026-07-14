namespace ProjectManager.Web.Models;

/// <summary>
/// 專案類型：分為保養和工程兩類。
/// </summary>
public enum ProjectType
{
    /// <summary>保養。</summary>
    Maintenance = 1,

    /// <summary>工程。</summary>
    Engineering = 2
}

/// <summary>
/// 專案主資料。所有專案列表、進度、請購、月結和審計記錄都圍繞該實體展開。
/// </summary>
public sealed class Project
{
    /// <summary>資料庫主鍵。</summary>
    public int Id { get; set; }

    /// <summary>專案年度；與專案工號組成未刪除專案的唯一業務鍵。</summary>
    public int Year { get; set; }

    /// <summary>專案類型。</summary>
    public ProjectType ProjectType { get; set; } = ProjectType.Engineering;

    /// <summary>母案案號，可为空。</summary>
    public string? ParentCaseNumber { get; set; }

    /// <summary>專案工號，同一年度内未刪除專案不可重复。</summary>
    public string ProjectNumber { get; set; } = string.Empty;

    /// <summary>專案名稱。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>專案進度百分比，业务規則限制在 0 到 100。</summary>
    public decimal ProgressPercent { get; set; }

    /// <summary>專案金額。</summary>
    public decimal ProjectAmount { get; set; }

    /// <summary>收款比例百分比。</summary>
    public decimal CollectionPercent { get; set; }

    /// <summary>進度补充說明。</summary>
    public string? ProgressDescription { get; set; }

    /// <summary>当前專案狀態外键。</summary>
    public int StatusId { get; set; }

    /// <summary>当前專案狀態導航屬性。</summary>
    public ProjectStatus? Status { get; set; }

    /// <summary>最近更新人的使用者 ID。</summary>
    public string? UpdatedByUserId { get; set; }

    /// <summary>最近更新人導航屬性。</summary>
    public ApplicationUser? UpdatedByUser { get; set; }

    /// <summary>结案年月；儲存时统一归一到该月第一天。</summary>
    public DateOnly? ClosedYearMonth { get; set; }

    /// <summary>廠商名稱（自由輸入，實施中專案使用）。</summary>
    public string? VendorName { get; set; }

    /// <summary>預計試車年月，僅精確到年月。</summary>
    public DateOnly? TrialRunYearMonth { get; set; }

    /// <summary>最近更新時間。</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>建立時間。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>用於偵測同一專案被多人同時修改的資料庫版本。</summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>软刪除标记；列表和明細查詢默认过滤已刪除專案。</summary>
    public bool IsDeleted { get; set; }

    /// <summary>分配到该專案的專案人員。</summary>
    public ICollection<ProjectAssignment> Assignments { get; } = new List<ProjectAssignment>();

    /// <summary>该專案下的請購記錄。</summary>
    public ICollection<PurchaseRequest> PurchaseRequests { get; } = new List<PurchaseRequest>();

    public ICollection<ProjectSkippedStatus> SkippedStatuses { get; } = new List<ProjectSkippedStatus>();

    public ProjectGanttPlan? GanttPlan { get; set; }

    public ICollection<ProjectCollaborationRecord> CollaborationRecords { get; } = new List<ProjectCollaborationRecord>();
}
