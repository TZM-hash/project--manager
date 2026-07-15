using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services.Workbench;

namespace ProjectManager.Web.Pages;

public class IndexModel(
    PersonalWorkbenchService personalWorkbenchService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public PersonalWorkbenchSnapshot? Workbench { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.CanManageAllBusinessData();
        Workbench = await personalWorkbenchService.BuildAsync(userId, canViewAll, cancellationToken);
    }
}
