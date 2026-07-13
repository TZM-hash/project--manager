namespace ProjectManager.Web.Models;

/// <summary>
/// 專案人員分配关系，控制普通專案人員能看到和更新哪些專案。
/// </summary>
public sealed class ProjectAssignment
{
    /// <summary>資料库主键。</summary>
    public int Id { get; set; }

    /// <summary>專案 ID。</summary>
    public int ProjectId { get; set; }

    /// <summary>專案導航屬性。</summary>
    public Project? Project { get; set; }

    /// <summary>人员使用者 ID。</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>人员導航屬性。</summary>
    public ApplicationUser? User { get; set; }

    /// <summary>專案内角色文本。</summary>
    public string RoleInProject { get; set; } = string.Empty;
}
