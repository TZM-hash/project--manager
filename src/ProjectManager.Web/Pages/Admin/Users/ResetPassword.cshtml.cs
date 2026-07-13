using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;

namespace ProjectManager.Web.Pages.Admin.Users;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class ResetPasswordModel(UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool IsWeakManaged { get; private set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (!await IsAdminUserAsync())
        {
            return Forbid();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        IsWeakManaged = user.IsWeakManaged;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        if (!await IsAdminUserAsync())
        {
            return Forbid();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        IsWeakManaged = user.IsWeakManaged;

        if (user.IsWeakManaged && string.IsNullOrWhiteSpace(Input.NewPassword))
        {
            return RedirectToPage("./Index");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var newPassword = string.IsNullOrWhiteSpace(Input.NewPassword)
            ? GenerateRandomPassword()
            : Input.NewPassword;

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            ModelState.AddModelError(string.Empty, "密碼不能為空。");
            return Page();
        }

        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        return RedirectToPage("./Index");
    }

    private static string GenerateRandomPassword()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 20)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    /// <summary>判断当前登入使用者是否为 admin 帳號（唯一可管理密碼的帳號）。</summary>
    private async Task<bool> IsAdminUserAsync()
    {
        var currentUserId = userManager.GetUserId(User);
        var currentUser = await userManager.FindByIdAsync(currentUserId ?? string.Empty);
        return string.Equals(currentUser?.UserName, "admin", StringComparison.OrdinalIgnoreCase);
    }

    public sealed class InputModel
    {
        [Display(Name = "新密碼")]
        public string NewPassword { get; set; } = string.Empty;
    }
}
