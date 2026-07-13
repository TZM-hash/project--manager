using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.Statuses;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class EditModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    AuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken cancellationToken)
    {
        if (id is null)
        {
            Input = new InputModel();
            return Page();
        }

        var status = await db.ProjectStatuses
            .AsNoTracking()
            .Include(x => x.Style)
            .SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken);

        if (status is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = status.Id,
            Code = status.Code,
            Name = status.Name,
            SortOrder = status.SortOrder,
            IsClosed = status.IsClosed,
            IsActive = status.IsActive,
            TextColor = status.Style?.TextColor ?? "#1f2937",
            BackgroundColor = status.Style?.BackgroundColor ?? "#e5e7eb",
            IsBold = status.Style?.IsBold ?? false
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id, CancellationToken cancellationToken)
    {
        if (id != Input.Id && Input.Id != 0)
        {
            return BadRequest();
        }

        await ValidateUniqueCodeAsync(cancellationToken);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        ProjectStatus status;
        var isNew = Input.Id == 0;
        if (Input.Id == 0)
        {
            status = new ProjectStatus
            {
                Style = new ProjectStatusStyle()
            };
            db.ProjectStatuses.Add(status);
        }
        else
        {
            status = await db.ProjectStatuses
                .Include(x => x.Style)
                .SingleOrDefaultAsync(x => x.Id == Input.Id, cancellationToken)
                ?? throw new InvalidOperationException("Status was not found while saving.");

            status.Style ??= new ProjectStatusStyle();
        }

        status.Code = Input.Code.Trim();
        status.Name = Input.Name.Trim();
        status.SortOrder = Input.SortOrder;
        status.IsClosed = Input.IsClosed;
        status.IsActive = Input.IsActive;
        status.Style.TextColor = Input.TextColor.Trim();
        status.Style.BackgroundColor = Input.BackgroundColor.Trim();
        status.Style.IsBold = Input.IsBold;

        await db.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(
            userManager.GetUserId(User),
            isNew ? "Create" : "Update",
            "ProjectStatus",
            status.Id.ToString(),
            $"Saved status {status.Code}.",
            cancellationToken);
        return RedirectToPage("./Index");
    }

    private async Task ValidateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        var code = Input.Code.Trim();
        var duplicate = await db.ProjectStatuses.AnyAsync(
            x => x.Code == code && x.Id != Input.Id,
            cancellationToken);

        if (duplicate)
        {
            ModelState.AddModelError("Input.Code", "狀態代码不能重复。");
        }
    }

    public sealed class InputModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(80)]
        public string Name { get; set; } = string.Empty;

        public int SortOrder { get; set; } = 100;

        public bool IsClosed { get; set; }

        public bool IsActive { get; set; } = true;

        [Required]
        [RegularExpression("^#[0-9A-Fa-f]{6}$")]
        public string TextColor { get; set; } = "#1f2937";

        [Required]
        [RegularExpression("^#[0-9A-Fa-f]{6}$")]
        public string BackgroundColor { get; set; } = "#e5e7eb";

        public bool IsBold { get; set; }
    }
}
