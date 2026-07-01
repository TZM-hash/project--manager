using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public sealed class IndexModel(UserManager<ApplicationUser> userManager) : PageModel
{
    public string UserName { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        UserName = user.UserName ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? "未填写" : user.DisplayName;
        Email = string.IsNullOrWhiteSpace(user.Email) ? "未填写" : user.Email;

        return Page();
    }
}
