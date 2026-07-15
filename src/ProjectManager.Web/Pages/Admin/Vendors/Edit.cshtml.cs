using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;

namespace ProjectManager.Web.Pages.Admin.Vendors;

[Authorize(Roles = RoleNames.BusinessManagerRoles)]
public sealed class EditModel(ApplicationDbContext db) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var vendor = await db.Vendors
            .Include(x => x.Contacts)
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (vendor is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = vendor.Id,
            CompanyName = vendor.CompanyName,
            Notes = vendor.Notes,
            Contacts = vendor.Contacts
                .Where(c => !c.IsDeleted)
                .Select(c => new ContactInputModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    Phone = c.Phone,
                    Notes = c.Notes
                })
                .ToList()
        };

        if (!Input.Contacts.Any())
        {
            Input.Contacts.Add(new ContactInputModel());
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var vendor = await db.Vendors
            .Include(x => x.Contacts)
            .SingleOrDefaultAsync(x => x.Id == Input.Id && !x.IsDeleted);

        if (vendor is null)
        {
            return NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        vendor.CompanyName = Input.CompanyName;
        vendor.Notes = Input.Notes;
        vendor.UpdatedAt = now;

        var existingById = vendor.Contacts.ToDictionary(c => c.Id);

        foreach (var contactInput in Input.Contacts)
        {
            if (contactInput.Id > 0 && existingById.TryGetValue(contactInput.Id, out var existing))
            {
                if (contactInput.IsDeleted)
                {
                    existing.IsDeleted = true;
                    existing.UpdatedAt = now;
                }
                else if (HasContactData(contactInput))
                {
                    existing.Name = contactInput.Name ?? string.Empty;
                    existing.Phone = contactInput.Phone;
                    existing.Notes = contactInput.Notes;
                    existing.UpdatedAt = now;
                }
            }
            else if (!contactInput.IsDeleted && HasContactData(contactInput))
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
        }

        await db.SaveChangesAsync();
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
        public int Id { get; set; }

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
        public int Id { get; set; }

        public bool IsDeleted { get; set; }

        [Display(Name = "姓名")]
        public string? Name { get; set; }

        [Display(Name = "電話")]
        [MaxLength(20)]
        public string? Phone { get; set; }

        [Display(Name = "備註")]
        public string? Notes { get; set; }
    }
}
