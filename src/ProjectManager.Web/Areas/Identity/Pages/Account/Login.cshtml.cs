using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel(SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ReturnUrl { get; private set; } = "/";

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl ?? Url.Content("~/");
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(
            Input.UserName.Trim(),
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            return LocalRedirect(ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "账号已锁定，请联系系统管理员。");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "账号或密码不正确。");
        return Page();
    }

    public sealed class InputModel
    {
        [Display(Name = "账号")]
        [Required(ErrorMessage = "请输入账号。")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "密码")]
        [Required(ErrorMessage = "请输入密码。")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "保持登录")]
        public bool RememberMe { get; set; } = true;
    }
}
