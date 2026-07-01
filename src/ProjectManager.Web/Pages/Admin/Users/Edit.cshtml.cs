using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;

namespace ProjectManager.Web.Pages.Admin.Users;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class EditModel(UserManager<ApplicationUser> userManager) : PageModel
{
    public IReadOnlyList<string> AvailableRoles { get; private set; } = RoleNames.All;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            UserName = user.UserName ?? string.Empty,
            DisplayName = user.DisplayName,
            Email = user.Email,
            IsActive = user.IsActive,
            SelectedRoles = [.. await userManager.GetRolesAsync(user)]
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.DisplayName = Input.DisplayName;
        user.Email = Input.Email;
        user.IsActive = Input.IsActive;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "用户保存失败，请检查邮箱格式和账号状态。");
            return Page();
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, currentRoles);
        var selectedRoles = Input.SelectedRoles.Intersect(RoleNames.All).ToArray();
        if (selectedRoles.Length > 0)
        {
            await userManager.AddToRolesAsync(user, selectedRoles);
        }

        return RedirectToPage("./Index");
    }

    public sealed class InputModel
    {
        [Display(Name = "用户名")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "姓名")]
        public string DisplayName { get; set; } = string.Empty;

        [Display(Name = "邮箱")]
        [EmailAddress(ErrorMessage = "邮箱格式不正确。")]
        public string? Email { get; set; }

        [Display(Name = "启用账号")]
        public bool IsActive { get; set; } = true;

        public List<string> SelectedRoles { get; set; } = [];
    }
}
