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
public sealed class EditModel(
    MaintenanceOrderService service,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> MethodOptions { get; } =
    [
        new SelectListItem("现场保養", "1"),
        new SelectListItem("远程保養", "2"),
        new SelectListItem("均有", "3")
    ];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var order = await service.GetOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = order.Id,
            Year = order.Year,
            CustomerName = order.CustomerName,
            MaintenanceStartDate = order.MaintenanceStartDate,
            MaintenanceEndDate = order.MaintenanceEndDate,
            MaintenanceMethod = order.MaintenanceMethod,
            OnSiteAnnualCount = order.OnSiteAnnualCount,
            RemoteAnnualCount = order.RemoteAnnualCount,
            ExecutorUserId = order.ExecutorUserId,
            HandoverPercent = order.HandoverPercent
        };

        await LoadUserOptionsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadUserOptionsAsync(cancellationToken);
            return Page();
        }

        var updated = await service.UpdateAsync(
            id,
            Input.Year,
            Input.CustomerName.Trim(),
            Input.MaintenanceStartDate,
            Input.MaintenanceEndDate,
            Input.MaintenanceMethod,
            Input.OnSiteAnnualCount,
            Input.RemoteAnnualCount,
            string.IsNullOrWhiteSpace(Input.ExecutorUserId) ? null : Input.ExecutorUserId,
            Input.HandoverPercent,
            userManager.GetUserId(User),
            cancellationToken);

        if (updated is null)
        {
            return NotFound();
        }

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
        public int Id { get; set; }

        [Display(Name = "年度")]
        [Required(ErrorMessage = "请输入年度。")]
        [Range(2000, 2100, ErrorMessage = "请输入有效年份。")]
        public int Year { get; set; }

        [Display(Name = "客戶名稱")]
        [Required(ErrorMessage = "请输入客戶名稱。")]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "保養開始日期")]
        [Required(ErrorMessage = "請選擇保養開始日期。")]
        public DateOnly MaintenanceStartDate { get; set; }

        [Display(Name = "保養結束日期")]
        [Required(ErrorMessage = "請選擇保養結束日期。")]
        public DateOnly MaintenanceEndDate { get; set; }

        [Display(Name = "保養方式")]
        [Required(ErrorMessage = "請選擇保養方式。")]
        public MaintenanceMethod MaintenanceMethod { get; set; }

        [Display(Name = "现场次數")]
        [Range(0, 100, ErrorMessage = "现场次數应在0-100之间。")]
        public int OnSiteAnnualCount { get; set; }

        [Display(Name = "远程次數")]
        [Range(0, 100, ErrorMessage = "远程次數应在0-100之间。")]
        public int RemoteAnnualCount { get; set; }

        [Display(Name = "保養执行人")]
        public string? ExecutorUserId { get; set; }

        [Display(Name = "签收单移交百分比")]
        [Range(0, 100, ErrorMessage = "移交百分比应在0-100之间。")]
        public decimal HandoverPercent { get; set; }
    }
}
