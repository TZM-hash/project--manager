using System.Reflection;
using System.Security.Claims;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;
using ProjectManager.Web.Security;
using AdminProjectImportModel = ProjectManager.Web.Pages.Admin.Projects.ImportModel;
using PlanningProjectImportModel = ProjectManager.Web.Pages.Workbench.PlanningProjects.ImportModel;

namespace ProjectManager.Tests.Web;

public sealed class ImportPageModelTests
{
    [Fact]
    public void Admin_project_import_year_is_bound_from_get_query()
    {
        var property = typeof(AdminProjectImportModel).GetProperty(nameof(AdminProjectImportModel.ImportYear))!;
        var bindProperty = property.GetCustomAttribute<BindPropertyAttribute>();

        bindProperty.Should().NotBeNull();
        bindProperty!.SupportsGet.Should().BeTrue();
    }

    [Fact]
    public async Task Admin_project_import_assigns_multiple_project_staff()
    {
        await using var services = await TestServices.CreateAsync();
        var db = services.Provider.GetRequiredService<ApplicationDbContext>();
        db.ProjectStatuses.Add(new ProjectStatus
        {
            Code = "Created",
            Name = "Created",
            SortOrder = 10,
            IsActive = true
        });
        db.Users.AddRange(
            new ApplicationUser { Id = "alice-id", UserName = "alice", DisplayName = "Alice" },
            new ApplicationUser { Id = "bob-id", UserName = "bob", DisplayName = "Bob" });
        await db.SaveChangesAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("ProjectsImport");
        sheet.Cell(1, 1).Value = "項次";
        sheet.Cell(1, 2).Value = "工程編號";
        sheet.Cell(1, 3).Value = "工程名稱";
        sheet.Cell(1, 4).Value = "經辦";
        sheet.Cell(1, 5).Value = "專案類型";
        sheet.Cell(1, 6).Value = "進度說明";
        sheet.Cell(1, 7).Value = "受訂金額（含稅）";
        sheet.Cell(2, 1).Value = 1;
        sheet.Cell(2, 2).Value = "P-2031-001";
        sheet.Cell(2, 3).Value = "Imported Project";
        sheet.Cell(2, 4).Value = "alice,bob";
        sheet.Cell(2, 5).Value = "工程";
        sheet.Cell(2, 6).Value = "Imported";
        sheet.Cell(2, 7).Value = 1200;

        var model = new AdminProjectImportModel(
            db,
            services.Provider.GetRequiredService<UserManager<ApplicationUser>>(),
            services.Provider.GetRequiredService<UserLookupService>(),
            services.Provider.GetRequiredService<AuditLogService>(),
            services.Provider.GetRequiredService<OpenCcConverterService>())
        {
            ImportYear = 2031,
            UploadFile = CreateWorkbookFile(workbook, "projects.xlsx")
        };
        model.PageContext = CreatePageContext();

        await model.OnPostAsync(CancellationToken.None);

        var assignments = await db.ProjectAssignments
            .OrderBy(x => x.UserId)
            .Select(x => x.UserId)
            .ToListAsync();
        assignments.Should().Equal("alice-id", "bob-id");
    }

    [Fact]
    public async Task Planning_project_import_rejects_xls_before_reading_workbook()
    {
        await using var services = await TestServices.CreateAsync();
        var model = new PlanningProjectImportModel(
            new PlanningProjectService(services.Provider.GetRequiredService<ApplicationDbContext>()),
            services.Provider.GetRequiredService<UserLookupService>())
        {
            UploadFile = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "UploadFile", "planning.xls")
        };

        await model.OnPostAsync(CancellationToken.None);

        model.ErrorMessage.Should().Contain(".xlsx");
    }

    [Fact]
    public void Planning_project_import_is_limited_to_business_managers()
    {
        var attribute = typeof(PlanningProjectImportModel)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attribute.Roles.Should().Be(RoleNames.BusinessManagerRoles);
    }

    [Fact]
    public async Task Planning_project_import_rejects_multiple_leaders_with_clear_error()
    {
        await using var services = await TestServices.CreateAsync();
        var db = services.Provider.GetRequiredService<ApplicationDbContext>();
        db.Users.AddRange(
            new ApplicationUser { Id = "alice-id", UserName = "alice", NormalizedUserName = "ALICE", IsActive = true },
            new ApplicationUser { Id = "bob-id", UserName = "bob", NormalizedUserName = "BOB", IsActive = true });
        await db.SaveChangesAsync();

        using var workbook = CreatePlanningWorkbook("alice,bob");
        var model = new PlanningProjectImportModel(
            new PlanningProjectService(db),
            services.Provider.GetRequiredService<UserLookupService>())
        {
            UploadFile = CreateWorkbookFile(workbook, "planning.xlsx")
        };

        await model.OnPostAsync(CancellationToken.None);

        model.ErrorMessage.Should().Contain("只能指定一位負責人");
        (await db.PlanningProjects.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Planning_project_import_rejects_user_without_project_staff_role()
    {
        await using var services = await TestServices.CreateAsync();
        var db = services.Provider.GetRequiredService<ApplicationDbContext>();
        db.Users.Add(new ApplicationUser
        {
            Id = "viewer-id",
            UserName = "viewer",
            NormalizedUserName = "VIEWER",
            IsActive = true
        });
        await db.SaveChangesAsync();

        using var workbook = CreatePlanningWorkbook("viewer");
        var model = new PlanningProjectImportModel(
            new PlanningProjectService(db),
            services.Provider.GetRequiredService<UserLookupService>())
        {
            UploadFile = CreateWorkbookFile(workbook, "planning.xlsx")
        };

        await model.OnPostAsync(CancellationToken.None);

        model.ErrorMessage.Should().Contain("有效的一般使用者");
        (await db.PlanningProjects.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Planning_project_template_uses_a_single_leader_example()
    {
        await using var services = await TestServices.CreateAsync();
        var model = new PlanningProjectImportModel(
            new PlanningProjectService(services.Provider.GetRequiredService<ApplicationDbContext>()),
            services.Provider.GetRequiredService<UserLookupService>());

        var file = model.OnGetTemplate().Should().BeOfType<FileContentResult>().Subject;
        using var stream = new MemoryStream(file.FileContents);
        using var workbook = new XLWorkbook(stream);

        workbook.Worksheet(1).Cell(2, 2).GetString().Should().NotContain(",");
    }

    private static XLWorkbook CreatePlanningWorkbook(string leaderUserName)
    {
        var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("PlanningProjects");
        sheet.Cell(1, 1).Value = "專案名";
        sheet.Cell(1, 2).Value = "專案負責人";
        sheet.Cell(1, 3).Value = "廠商";
        sheet.Cell(1, 4).Value = "最新說明";
        sheet.Cell(2, 1).Value = "Test Planning Project";
        sheet.Cell(2, 2).Value = leaderUserName;
        return workbook;
    }

    private static PageContext CreatePageContext()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };

        return new PageContext
        {
            HttpContext = httpContext
        };
    }

    private static IFormFile CreateWorkbookFile(XLWorkbook workbook, string fileName)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return new FormFile(stream, 0, stream.Length, "UploadFile", fileName);
    }

    private sealed class TestServices : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private TestServices(SqliteConnection connection, ServiceProvider provider)
        {
            this.connection = connection;
            Provider = provider;
        }

        public ServiceProvider Provider { get; }

        public static async Task<TestServices> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(connection);
            services.AddDbContext<ApplicationDbContext>((provider, options) =>
                options.UseSqlite(provider.GetRequiredService<SqliteConnection>()));
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddScoped<UserLookupService>();
            services.AddScoped<AuditLogService>();
            services.AddSingleton<OpenCcConverterService>();

            var provider = services.BuildServiceProvider();
            var db = provider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();

            return new TestServices(connection, provider);
        }

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
