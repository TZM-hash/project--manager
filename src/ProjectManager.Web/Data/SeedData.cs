using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;

namespace ProjectManager.Web.Data;

public static class SeedData
{
    public static IReadOnlyList<InitialProjectStatus> InitialStatuses { get; } =
    [
        new("Created", "已立案", 10, false),
        new("PurchaseRequested", "已請購", 20, false),
        new("Coding", "程式中", 30, false),
        new("TrialRun", "試車中", 40, false),
        new("WaitingCollection", "待收款", 50, false),
        new("PendingClosure", "待結案", 55, false),
        new("Closed", "已結案", 60, true)
    ];

    public static IReadOnlyList<InitialStatusStyle> InitialStatusStyles { get; } =
    [
        new("Created", "#1f2937", "#e5e7eb", false),
        new("PurchaseRequested", "#1d4ed8", "#dbeafe", false),
        new("Coding", "#7c2d12", "#ffedd5", false),
        new("TrialRun", "#047857", "#d1fae5", false),
        new("WaitingCollection", "#854d0e", "#fef3c7", true),
        new("PendingClosure", "#7c3aed", "#ede9fe", true),
        new("Closed", "#dc2626", "#fee2e2", true)
    ];

    public static async Task EnsureSeededAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;

        var roleManager = scopedServices.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scopedServices.GetRequiredService<ApplicationDbContext>();
        var configuration = scopedServices.GetRequiredService<IConfiguration>();

        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            await db.Database.MigrateAsync();
        }

        await SeedRolesAsync(roleManager);
        await SeedStatusesAsync(db);
        await SeedAdminUserAsync(userManager, configuration);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task SeedStatusesAsync(ApplicationDbContext db)
    {
        foreach (var initialStatus in InitialStatuses)
        {
            var status = await db.ProjectStatuses
                .Include(x => x.Style)
                .SingleOrDefaultAsync(x => x.Code == initialStatus.Code);

            if (status is null)
            {
                status = new ProjectStatus
                {
                    Code = initialStatus.Code,
                    Name = initialStatus.Name,
                    SortOrder = initialStatus.SortOrder,
                    IsClosed = initialStatus.IsClosed,
                    IsActive = true
                };
                db.ProjectStatuses.Add(status);
            }
            else
            {
                status.Name = initialStatus.Name;
                status.SortOrder = initialStatus.SortOrder;
                status.IsClosed = initialStatus.IsClosed;
                status.IsActive = true;
            }

            var styleDefinition = InitialStatusStyles.Single(x => x.StatusCode == initialStatus.Code);
            status.Style ??= new ProjectStatusStyle();
            status.Style.TextColor = styleDefinition.TextColor;
            status.Style.BackgroundColor = styleDefinition.BackgroundColor;
            status.Style.IsBold = styleDefinition.IsBold;
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        var adminSection = configuration.GetSection("AdminSeed");
        var userName = adminSection["UserName"] ?? "admin";
        var email = adminSection["Email"] ?? "admin@example.local";
        var password = adminSection["Password"] ?? "ChangeMe123!";

        var admin = await userManager.FindByNameAsync(userName);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                DisplayName = "系統管理員",
                EmailConfirmed = true,
                IsActive = true
            };

            var createResult = await userManager.CreateAsync(admin, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to create seed admin user: " +
                    string.Join("; ", createResult.Errors.Select(x => x.Description)));
            }
        }
        else if (string.IsNullOrWhiteSpace(admin.DisplayName) || admin.DisplayName == "Administrator")
        {
            admin.DisplayName = "系統管理員";
            await userManager.UpdateAsync(admin);
        }

        if (!await userManager.IsInRoleAsync(admin, RoleNames.Administrator))
        {
            await userManager.AddToRoleAsync(admin, RoleNames.Administrator);
        }
    }
}

public sealed record InitialProjectStatus(
    string Code,
    string Name,
    int SortOrder,
    bool IsClosed);

public sealed record InitialStatusStyle(
    string StatusCode,
    string TextColor,
    string BackgroundColor,
    bool IsBold);
