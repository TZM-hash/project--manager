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

[Authorize(Roles = RoleNames.BusinessManagerRoles)]
public sealed class CreateModel(
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

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadUserOptionsAsync(cancellationToken);
        Input.Year = DateTime.Today.Year;
        Input.MaintenanceStartDate = new DateOnly(DateTime.Today.Year, 1, 1);
        Input.MaintenanceEndDate = new DateOnly(DateTime.Today.Year, 12, 31);
        Input.SiteName = "主厂区";
        Input.OnSiteSoftwareFrequency = "半年/次";
        Input.OnSiteHardwareFrequency = "一年/次";
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (Input.MaintenanceEndDate < Input.MaintenanceStartDate)
        {
            ModelState.AddModelError("Input.MaintenanceEndDate", "保養结束日期不得早于開始日期。");
        }

        if (!ModelState.IsValid)
        {
            await LoadUserOptionsAsync(cancellationToken);
            return Page();
        }

        var order = new MaintenanceOrder
        {
            Year = Input.Year,
            ContractNumber = Input.ContractNumber.Trim(),
            CustomerName = Input.CustomerName.Trim(),
            SiteName = Input.SiteName.Trim(),
            MaintenanceStartDate = Input.MaintenanceStartDate,
            MaintenanceEndDate = Input.MaintenanceEndDate,
            MaintenanceMethod = Input.MaintenanceMethod,
            OnSiteAnnualCount = Input.OnSiteAnnualCount,
            RemoteAnnualCount = Input.RemoteAnnualCount,
            OnSiteSoftwareFrequency = Input.OnSiteSoftwareFrequency.Trim(),
            OnSiteHardwareFrequency = Input.OnSiteHardwareFrequency.Trim(),
            ExecutorUserId = string.IsNullOrWhiteSpace(Input.ExecutorUserId) ? null : Input.ExecutorUserId,
            ProgressPercent = Input.ProgressPercent,
            HandoverPercent = Input.HandoverPercent,
            MaintenanceDescription = Input.MaintenanceDescription.Trim()
        };

        await service.CreateAsync(order, userManager.GetUserId(User), cancellationToken);
        TempData["SuccessMessage"] = $"保養訂單「{order.ContractNumber}」已新增。";
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

        [Display(Name = "合約編號")]
        [Required(ErrorMessage = "请输入合約編號。")]
        [StringLength(50, ErrorMessage = "合約編號不可超过50个字。")]
        public string ContractNumber { get; set; } = string.Empty;

        [Display(Name = "客戶名稱")]
        [Required(ErrorMessage = "请输入客戶名稱。")]
        [StringLength(200, ErrorMessage = "客戶名稱不可超过200个字。")]
        public string CustomerName { get; set; } = string.Empty;

        [Display(Name = "厂区/據點")]
        [Required(ErrorMessage = "请输入厂区或據點。")]
        [StringLength(100, ErrorMessage = "厂区或據點不可超过100个字。")]
        public string SiteName { get; set; } = string.Empty;

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

        [Display(Name = "现场软件保養频率")]
        [Required(ErrorMessage = "请输入现场软件保養频率，无则填写“无”。")]
        [StringLength(50, ErrorMessage = "现场软件保養频率不可超过50个字。")]
        public string OnSiteSoftwareFrequency { get; set; } = string.Empty;

        [Display(Name = "现场硬件保養频率")]
        [Required(ErrorMessage = "请输入现场硬件保養频率，无则填写“无”。")]
        [StringLength(50, ErrorMessage = "现场硬件保養频率不可超过50个字。")]
        public string OnSiteHardwareFrequency { get; set; } = string.Empty;

        [Display(Name = "保養执行人")]
        public string? ExecutorUserId { get; set; }

        [Display(Name = "保養進度")]
        [Range(0, 100, ErrorMessage = "保養進度应在0-100之间。")]
        public decimal ProgressPercent { get; set; }

        [Display(Name = "签收单移交百分比")]
        [Range(0, 100, ErrorMessage = "移交百分比应在0-100之间。")]
        public decimal HandoverPercent { get; set; }

        [Display(Name = "保養内容说明")]
        [Required(ErrorMessage = "请输入保養内容说明。")]
        [StringLength(1000, ErrorMessage = "保養内容说明不可超过1000个字。")]
        public string MaintenanceDescription { get; set; } = string.Empty;
    }
}
