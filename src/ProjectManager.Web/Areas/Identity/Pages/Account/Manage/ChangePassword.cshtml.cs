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
            ModelState.AddModelError(string.Empty, "密碼修改失敗，请確認原密碼正确，并检查新密碼复杂度。");
            return Page();
        }

        await signInManager.RefreshSignInAsync(user);
        StatusMessage = "密碼已更新。";
        return RedirectToPage();
    }

    public sealed class InputModel
    {
        [Display(Name = "原密碼")]
        [Required(ErrorMessage = "请输入原密碼。")]
        [DataType(DataType.Password)]
        public string OldPassword { get; set; } = string.Empty;

        [Display(Name = "新密碼")]
        [Required(ErrorMessage = "请输入新密碼。")]
        [StringLength(100, ErrorMessage = "新密碼至少需要 {2} 个字符。", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Display(Name = "確認新密碼")]
        [Required(ErrorMessage = "请再次输入新密碼。")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "两次输入的新密碼不一致。")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
