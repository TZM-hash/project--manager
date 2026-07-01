using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;

namespace ProjectManager.Web.Pages.Admin.Users;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class IndexModel(UserManager<ApplicationUser> userManager) : PageModel
{
    public IList<UserListItem> Users { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var users = await userManager.Users
            .OrderBy(x => x.UserName)
            .ToListAsync();

        var rows = new List<UserListItem>();
        foreach (var user in users)
        {
            rows.Add(new UserListItem(
                user.Id,
                user.UserName ?? string.Empty,
                user.DisplayName,
                user.Email ?? string.Empty,
                user.IsActive,
                await userManager.GetRolesAsync(user)));
        }

        Users = rows;
    }

    public sealed record UserListItem(
        string Id,
        string UserName,
        string DisplayName,
        string Email,
        bool IsActive,
        IList<string> Roles);
}
