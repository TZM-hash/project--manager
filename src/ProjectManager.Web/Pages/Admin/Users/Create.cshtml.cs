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
        if (Input.IsWeakManaged)
        {
            ModelState.Remove("Input.Password");
        }

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
            IsActive = true,
            IsWeakManaged = Input.IsWeakManaged
        };

        var password = Input.IsWeakManaged && string.IsNullOrWhiteSpace(Input.Password)
            ? GenerateRandomPassword()
            : Input.Password;

        if (string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(string.Empty, "密碼不能為空。");
            return Page();
        }

        var createResult = await userManager.CreateAsync(user, password);

        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        var selectedRoles = Input.SelectedRoles.Intersect(RoleNames.All).ToArray();
        if (selectedRoles.Length > 0)
        {
            await userManager.AddToRolesAsync(user, selectedRoles);
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

    public sealed class InputModel
    {
        [Display(Name = "使用者名稱")]
        [Required(ErrorMessage = "请输入使用者名稱。")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "姓名")]
        public string DisplayName { get; set; } = string.Empty;

        [Display(Name = "信箱")]
        [EmailAddress(ErrorMessage = "信箱格式不正确。")]
        public string? Email { get; set; }

        [Display(Name = "初始密碼")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "弱管理")]
        public bool IsWeakManaged { get; set; }

        public List<string> SelectedRoles { get; set; } = [];
    }
}
