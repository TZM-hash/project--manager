using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;

namespace ProjectManager.Web.Pages.Admin.Users;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class EditModel(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db) : PageModel
{
    public IReadOnlyList<string> AvailableRoles { get; private set; } = RoleNames.Assignable;

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
            IsWeakManaged = user.IsWeakManaged,
            SelectedRoles = [.. await userManager.GetRolesAsync(user)]
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var roleResult = RoleSelection.Normalize(Input.SelectedRoles);
        if (!roleResult.Succeeded)
        {
            ModelState.AddModelError(nameof(Input.SelectedRoles), roleResult.ErrorMessage!);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
        var currentRoles = await userManager.GetRolesAsync(user);
        if (await WouldRemoveLastActiveAdministratorAsync(user, currentRoles, roleResult.Roles))
        {
            ModelState.AddModelError(string.Empty, "系統至少需要保留一個啟用中的系統管理員。");
            return Page();
        }

        user.DisplayName = Input.DisplayName;
        user.Email = Input.Email;
        user.IsActive = Input.IsActive;
        user.IsWeakManaged = Input.IsWeakManaged;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            AddIdentityErrors(updateResult);
            return Page();
        }

        var rolesToAdd = roleResult.Roles.Except(currentRoles, StringComparer.Ordinal).ToArray();
        if (rolesToAdd.Length > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(addResult);
                return Page();
            }
        }

        var rolesToRemove = currentRoles.Except(roleResult.Roles, StringComparer.Ordinal).ToArray();
        if (rolesToRemove.Length > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(removeResult);
                return Page();
            }
        }

        await transaction.CommitAsync();
        TempData["SuccessMessage"] = $"使用者「{user.UserName}」已更新。";
        return RedirectToPage("./Index");
    }

    private async Task<bool> WouldRemoveLastActiveAdministratorAsync(
        ApplicationUser user,
        IEnumerable<string> currentRoles,
        IEnumerable<string> selectedRoles)
    {
        if (!currentRoles.Contains(RoleNames.Administrator) ||
            (Input.IsActive && selectedRoles.Contains(RoleNames.Administrator)))
        {
            return false;
        }

        var administrators = await userManager.GetUsersInRoleAsync(RoleNames.Administrator);
        return administrators.All(x => x.Id == user.Id || !x.IsActive);
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    public sealed class InputModel
    {
        [Display(Name = "使用者名稱")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "姓名")]
        public string DisplayName { get; set; } = string.Empty;

        [Display(Name = "信箱")]
        [EmailAddress(ErrorMessage = "信箱格式不正确。")]
        public string? Email { get; set; }

        [Display(Name = "啟用帳號")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "弱管理")]
        public bool IsWeakManaged { get; set; }

        public List<string> SelectedRoles { get; set; } = [];
    }
}
