using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.MaintenanceOrders;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class CreateModel(
    MaintenanceOrderService service,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> MethodOptions { get; } =
    [
        new SelectListItem("现场保养", "1"),
        new SelectListItem("远程保养", "2"),
        new SelectListItem("均有", "3")
    ];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadUserOptionsAsync(cancellationToken);
        Input.Year = DateTime.Today.Year;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadUserOptionsAsync(cancellationToken);
            return Page();
        }

        var order = new MaintenanceOrder
        {
            Year = Input.Year,
            CustomerName = Input.CustomerName.Trim(),
            MaintenanceStartDate = Input.MaintenanceStartDate,
            MaintenanceEndDate = Input.MaintenanceEndDate,
            MaintenanceMethod = Input.MaintenanceMethod,
            OnSiteAnnualCount = Input.OnSiteAnnualCount,
            RemoteAnnualCount = Input.RemoteAnnualCount,
            ExecutorUserId = string.IsNullOrWhiteSpace(Input.ExecutorUserId) ? null : Input.ExecutorUserId,
            HandoverPercent = Input.HandoverPercent
        };

        await service.CreateAsync(order, userManager.GetUserId(User), cancellationToken);
        return RedirectToPage("./Index");
    }

    private async Task LoadUserOptionsAsync(CancellationToken cancellationToken)
    {
        var users = await userManager.Users
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        UserOptions = users
            .Select(x => new SelectListItem(
                string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? x.Id : x.DisplayName,
                x.Id))
            .ToList();
    }

    public sealed class InputModel
    {
        [Display(Name = "年度")]
        [Required(ErrorMessage = "请输入年度。")]
        [Range(2000, 2100, ErrorMessage = "请输入有效年份。")]
        public int Year { get; set; }

        [Display(Name = "客户名称")]
        [Required(ErrorMessage = "请输入客户名称。")]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "保养开始日期")]
        [Required(ErrorMessage = "请选择保养开始日期。")]
        public DateOnly MaintenanceStartDate { get; set; }

        [Display(Name = "保养结束日期")]
        [Required(ErrorMessage = "请选择保养结束日期。")]
        public DateOnly MaintenanceEndDate { get; set; }

        [Display(Name = "保养方式")]
        [Required(ErrorMessage = "请选择保养方式。")]
        public MaintenanceMethod MaintenanceMethod { get; set; }

        [Display(Name = "现场次数")]
        [Range(0, 100, ErrorMessage = "现场次数应在0-100之间。")]
        public int OnSiteAnnualCount { get; set; }

        [Display(Name = "远程次数")]
        [Range(0, 100, ErrorMessage = "远程次数应在0-100之间。")]
        public int RemoteAnnualCount { get; set; }

        [Display(Name = "保养执行人")]
        public string? ExecutorUserId { get; set; }

        [Display(Name = "签收单移交百分比")]
        [Range(0, 100, ErrorMessage = "移交百分比应在0-100之间。")]
        public decimal HandoverPercent { get; set; }
    }
}
