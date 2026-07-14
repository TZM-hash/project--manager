using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services.Operations;

namespace ProjectManager.Web.Pages.Admin.Operations;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class IndexModel(OperationalHealthService healthService) : PageModel
{
    public OperationalHealthSnapshot Snapshot { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Snapshot = await healthService.BuildAsync(cancellationToken);
    }

    public static string LevelText(OperationalStatusLevel level) => level switch
    {
        OperationalStatusLevel.Healthy => "正常",
        OperationalStatusLevel.Warning => "注意",
        OperationalStatusLevel.Critical => "異常",
        _ => "未知"
    };

    public static string LevelClass(OperationalStatusLevel level) => level switch
    {
        OperationalStatusLevel.Healthy => "healthy",
        OperationalStatusLevel.Warning => "warning",
        OperationalStatusLevel.Critical => "critical",
        _ => "unknown"
    };
}
