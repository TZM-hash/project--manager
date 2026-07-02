namespace ProjectManager.Web.Models;

/// <summary>
/// 项目人员分配关系，控制普通专案人员能看到和更新哪些项目。
/// </summary>
public sealed class ProjectAssignment
{
    /// <summary>数据库主键。</summary>
    public int Id { get; set; }

    /// <summary>项目 ID。</summary>
    public int ProjectId { get; set; }

    /// <summary>项目导航属性。</summary>
    public Project? Project { get; set; }

    /// <summary>人员用户 ID。</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>人员导航属性。</summary>
    public ApplicationUser? User { get; set; }

    /// <summary>项目内角色文本。</summary>
    public string RoleInProject { get; set; } = string.Empty;
}
