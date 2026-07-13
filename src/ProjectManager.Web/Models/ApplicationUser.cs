using Microsoft.AspNetCore.Identity;

namespace ProjectManager.Web.Models;

/// <summary>
/// 系统使用者。继承 IdentityUser，并扩展业务顯示名稱和啟用狀態。
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
    /// <summary>頁面优先顯示的中文姓名或昵称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>帳號是否啟用；停用帳號不会出现在业务選擇列表中。</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>帳號建立時間。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>是否为弱管理帳號；弱管理帳號不强制要求密碼和信箱。</summary>
    public bool IsWeakManaged { get; set; }
}
