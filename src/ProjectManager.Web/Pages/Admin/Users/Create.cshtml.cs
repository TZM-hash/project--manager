using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;

namespace ProjectManager.Web.Pages.Admin.Users;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class CreateModel(UserManager<ApplicationUser> userManager) : PageModel
{
    public IReadOnlyList<string> AvailableRoles { get; private set; } = RoleNames.All;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = Input.UserName,
            Email = Input.Email,
            DisplayName = Input.DisplayName,
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(user, Input.Password);
        if (!createResult.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "用户创建失败，请检查用户名、邮箱和密码复杂度。");
            return Page();
        }

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
        [Required(ErrorMessage = "请输入用户名。")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "姓名")]
        public string DisplayName { get; set; } = string.Empty;

        [Display(Name = "邮箱")]
        [EmailAddress(ErrorMessage = "邮箱格式不正确。")]
        public string? Email { get; set; }

        [Display(Name = "初始密码")]
        [Required(ErrorMessage = "请输入初始密码。")]
        public string Password { get; set; } = string.Empty;

        public List<string> SelectedRoles { get; set; } = [];
    }
}
