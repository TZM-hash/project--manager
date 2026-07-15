using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;

namespace ProjectManager.Web.Pages.Admin.Vendors;

[Authorize(Roles = RoleNames.BusinessManagerRoles)]
public sealed class CreateModel(ApplicationDbContext db) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
        Input.Contacts.Add(new ContactInputModel());
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var now = DateTimeOffset.UtcNow;
        var vendor = new Vendor
        {
            CompanyName = Input.CompanyName,
            Notes = Input.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var contactInput in Input.Contacts.Where(HasContactData))
        {
            vendor.Contacts.Add(new VendorContact
            {
                Name = contactInput.Name ?? string.Empty,
                Phone = contactInput.Phone,
                Notes = contactInput.Notes,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        db.Vendors.Add(vendor);
        await db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"廠商「{vendor.CompanyName}」已新增。";
        return RedirectToPage("./Index");
    }

    private static bool HasContactData(ContactInputModel input)
    {
        return !string.IsNullOrWhiteSpace(input.Name) ||
               !string.IsNullOrWhiteSpace(input.Phone) ||
               !string.IsNullOrWhiteSpace(input.Notes);
    }

    public sealed class InputModel
    {
        [Display(Name = "公司名稱")]
        [Required(ErrorMessage = "请输入公司名稱。")]
        [MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [Display(Name = "備註")]
        public string? Notes { get; set; }

        public List<ContactInputModel> Contacts { get; set; } = [];
    }

    public sealed class ContactInputModel
    {
        [Display(Name = "姓名")]
        public string? Name { get; set; }

        [Display(Name = "電話")]
        [MaxLength(20)]
        public string? Phone { get; set; }

        [Display(Name = "備註")]
        public string? Notes { get; set; }
    }
}
