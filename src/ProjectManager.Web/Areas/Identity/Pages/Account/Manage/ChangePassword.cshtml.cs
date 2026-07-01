using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public sealed class ChangePasswordModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "密码修改失败，请确认原密码正确，并检查新密码复杂度。");
            return Page();
        }

        await signInManager.RefreshSignInAsync(user);
        StatusMessage = "密码已更新。";
        return RedirectToPage();
    }

    public sealed class InputModel
    {
        [Display(Name = "原密码")]
        [Required(ErrorMessage = "请输入原密码。")]
        [DataType(DataType.Password)]
        public string OldPassword { get; set; } = string.Empty;

        [Display(Name = "新密码")]
        [Required(ErrorMessage = "请输入新密码。")]
        [StringLength(100, ErrorMessage = "新密码至少需要 {2} 个字符。", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Display(Name = "确认新密码")]
        [Required(ErrorMessage = "请再次输入新密码。")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "两次输入的新密码不一致。")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
