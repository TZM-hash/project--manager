using Microsoft.AspNetCore.Identity;

namespace ProjectManager.Web.Models;

/// <summary>
/// 系统用户。继承 IdentityUser，并扩展业务显示名称和启用状态。
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
    /// <summary>页面优先显示的中文姓名或昵称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>账号是否启用；停用账号不会出现在业务选择列表中。</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>账号创建时间。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
