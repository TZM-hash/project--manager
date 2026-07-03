using System.Reflection;
using System.Security.Claims;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;
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
        sheet.Cell(1, 1).Value = "Project Number";
        sheet.Cell(1, 2).Value = "Project Name";
        sheet.Cell(1, 3).Value = "Project Staff";
        sheet.Cell(1, 4).Value = "Amount";
        sheet.Cell(1, 5).Value = "Progress Note";
        sheet.Cell(2, 1).Value = "P-2031-001";
        sheet.Cell(2, 2).Value = "Imported Project";
        sheet.Cell(2, 3).Value = "alice,bob";
        sheet.Cell(2, 4).Value = 1200;
        sheet.Cell(2, 5).Value = "Imported";

        var model = new AdminProjectImportModel(
            db,
            services.Provider.GetRequiredService<UserManager<ApplicationUser>>(),
            services.Provider.GetRequiredService<UserLookupService>(),
            services.Provider.GetRequiredService<AuditLogService>())
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
