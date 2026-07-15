using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;

namespace ProjectManager.Tests.Web;

public sealed class PermissionHierarchySmokeTests
{
    private static readonly string[] BusinessManagerPages =
    [
        "/Admin/ProjectAssignments",
        "/Admin/Projects",
        "/Admin/MaintenanceOrders",
        "/Admin/Vendors",
        "/Admin/Statuses",
        "/Admin/DataExchange",
        "/Workbench/PlanningProjects",
        "/Settlements"
    ];

    private static readonly string[] SystemAdministratorPages =
    [
        "/Admin/Users",
        "/Admin/Settings",
        "/Admin/Operations"
    ];

    [Fact]
    public async Task Information_administrator_can_manage_business_pages()
    {
        await using var factory = new PermissionWebFactory();
        var client = CreateClient(factory, RoleNames.Leader);

        foreach (var page in BusinessManagerPages)
        {
            var response = await client.GetAsync(page);
            response.StatusCode.Should().Be(HttpStatusCode.OK, page);
        }
    }

    [Fact]
    public async Task Information_administrator_cannot_manage_system_pages()
    {
        await using var factory = new PermissionWebFactory();
        var client = CreateClient(factory, RoleNames.Leader);

        foreach (var page in SystemAdministratorPages)
        {
            var response = await client.GetAsync(page);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, page);
        }
    }

    [Fact]
    public void Information_administrator_is_authorized_for_archives()
    {
        var attribute = typeof(ProjectManager.Web.Pages.Admin.Archives.IndexModel)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();

        attribute.Roles.Should().Be(RoleNames.FullBusinessReadRoles);
    }

    [Theory]
    [InlineData(RoleNames.Administrator)]
    [InlineData(RoleNames.Leader)]
    public async Task Business_managers_see_all_planning_projects(string role)
    {
        await using var factory = new PermissionWebFactory();
        await factory.SeedPlanningProjectsAsync();
        var client = CreateClient(factory, role);

        var response = await client.GetAsync("/Workbench/PlanningProjects");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("MY-PLANNING-PROJECT");
        html.Should().Contain("OTHER-PLANNING-PROJECT");
    }

    [Fact]
    public async Task Data_viewer_can_read_all_project_and_planning_views_without_edit_controls()
    {
        await using var factory = new PermissionWebFactory();
        var ids = await factory.SeedScopedProjectsAsync();
        var client = CreateClient(factory, RoleNames.DataViewer);

        var projects = await client.GetAsync("/Workbench/Projects");
        var projectsHtml = WebUtility.HtmlDecode(await projects.Content.ReadAsStringAsync());
        var details = await client.GetAsync($"/Workbench/Projects/Details/{ids.OtherProjectId}");
        var detailsHtml = WebUtility.HtmlDecode(await details.Content.ReadAsStringAsync());
        var ganttPrint = await client.GetAsync($"/Workbench/Projects/GanttPrint/{ids.OtherProjectId}");
        var planning = await client.GetAsync("/Workbench/PlanningProjects");
        var planningHtml = WebUtility.HtmlDecode(await planning.Content.ReadAsStringAsync());
        var planningPrint = await client.GetAsync($"/Workbench/PlanningProjects/Print/{ids.OtherPlanningProjectId}");
        var planningPrintList = await client.GetAsync(
            $"/Workbench/PlanningProjects/PrintList?Ids={ids.OwnPlanningProjectId},{ids.OtherPlanningProjectId}");
        var planningPrintListHtml = WebUtility.HtmlDecode(await planningPrintList.Content.ReadAsStringAsync());

        projects.StatusCode.Should().Be(HttpStatusCode.OK);
        projectsHtml.Should().Contain("專案查詢");
        projectsHtml.Should().NotContain("<h1 class=\"page-title\">我的專案</h1>");
        projectsHtml.Should().Contain("MY-SCOPED-PROJECT");
        projectsHtml.Should().Contain("OTHER-SCOPED-PROJECT");
        projectsHtml.Should().NotContain(">編輯<");
        projectsHtml.Should().NotContain("儲存目前檢視");
        details.StatusCode.Should().Be(HttpStatusCode.OK);
        detailsHtml.Should().Contain("OTHER-SCOPED-PROJECT");
        detailsHtml.Should().NotContain("編輯專案");
        ganttPrint.StatusCode.Should().Be(HttpStatusCode.OK);
        planning.StatusCode.Should().Be(HttpStatusCode.OK);
        planningHtml.Should().Contain("MY-SCOPED-PLANNING");
        planningHtml.Should().Contain("OTHER-SCOPED-PLANNING");
        planningHtml.Should().NotContain(">新增<");
        planningHtml.Should().NotContain(">編輯<");
        planningHtml.Should().NotContain("批量刪除");
        planningPrint.StatusCode.Should().Be(HttpStatusCode.OK);
        planningPrintList.StatusCode.Should().Be(HttpStatusCode.OK);
        planningPrintListHtml.Should().Contain("MY-SCOPED-PLANNING");
        planningPrintListHtml.Should().Contain("OTHER-SCOPED-PLANNING");
    }

    [Fact]
    public async Task Data_viewer_can_open_read_only_business_pages_but_not_system_or_write_actions()
    {
        await using var factory = new PermissionWebFactory();
        var client = CreateClient(factory, RoleNames.DataViewer);

        var maintenance = await client.GetAsync("/Admin/MaintenanceOrders");
        var maintenanceHtml = WebUtility.HtmlDecode(await maintenance.Content.ReadAsStringAsync());
        var settlements = await client.GetAsync("/Settlements");
        var settlementsHtml = WebUtility.HtmlDecode(await settlements.Content.ReadAsStringAsync());
        var archives = await client.GetAsync("/Admin/Archives");
        var archivesHtml = WebUtility.HtmlDecode(await archives.Content.ReadAsStringAsync());

        maintenance.StatusCode.Should().Be(HttpStatusCode.OK);
        maintenanceHtml.Should().NotContain(">新增<");
        maintenanceHtml.Should().NotContain(">編輯<");
        maintenanceHtml.Should().NotContain("批量刪除");
        settlements.StatusCode.Should().Be(HttpStatusCode.OK);
        settlementsHtml.Should().NotContain("新增月結");
        settlementsHtml.Should().NotContain("批量刪除");
        archives.StatusCode.Should().Be(HttpStatusCode.OK, archivesHtml);
        archivesHtml.Should().NotContain(">還原<");

        foreach (var page in new[]
                 {
                     "/Admin/Projects",
                     "/Admin/Users",
                     "/Admin/Statuses",
                     "/Admin/Vendors",
                     "/Admin/DataExchange",
                     "/Admin/Operations",
                     "/Admin/Settings"
                 })
        {
            (await client.GetAsync(page)).StatusCode.Should().Be(HttpStatusCode.Forbidden, page);
        }

        var planning = await client.GetAsync("/Workbench/PlanningProjects");
        var token = ExtractAntiforgeryToken(await planning.Content.ReadAsStringAsync());
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ids"] = "1"
        };
        (await client.PostAsync("/Admin/MaintenanceOrders?handler=BatchDelete", new FormUrlEncodedContent(form)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync("/Settlements?handler=BatchDelete", new FormUrlEncodedContent(form)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync("/Admin/Archives?handler=Restore&id=1", new FormUrlEncodedContent(form)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync("/Workbench/Projects?handler=SaveView", new FormUrlEncodedContent(form)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync("/Reports/OpenProjects?handler=SaveView", new FormUrlEncodedContent(form)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(RoleNames.ProjectStaff)]
    public async Task Regular_users_only_see_their_planning_projects(string role)
    {
        await using var factory = new PermissionWebFactory();
        await factory.SeedPlanningProjectsAsync();
        var client = CreateClient(factory, role);

        var response = await client.GetAsync("/Workbench/PlanningProjects");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("MY-PLANNING-PROJECT");
        html.Should().NotContain("OTHER-PLANNING-PROJECT");
        html.Should().NotContain("COLLIDING-USER-ID-PLANNING-PROJECT");

        var forgedResponse = await client.GetAsync("/Workbench/PlanningProjects?LeaderUserId=other-user");
        var forgedHtml = WebUtility.HtmlDecode(await forgedResponse.Content.ReadAsStringAsync());

        forgedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        forgedHtml.Should().Contain("MY-PLANNING-PROJECT");
        forgedHtml.Should().NotContain("OTHER-PLANNING-PROJECT");
    }

    [Theory]
    [InlineData(RoleNames.Administrator)]
    [InlineData(RoleNames.Leader)]
    public async Task Business_managers_can_edit_any_project_and_planning_project(string role)
    {
        await using var factory = new PermissionWebFactory();
        var ids = await factory.SeedScopedProjectsAsync();
        var client = CreateClient(factory, role);

        var projectResponse = await client.GetAsync($"/Admin/Projects/Edit/{ids.OtherProjectId}");
        var planningResponse = await client.GetAsync($"/Workbench/PlanningProjects/Edit/{ids.OtherPlanningProjectId}");

        projectResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        planningResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Regular_user_cannot_edit_or_list_other_users_projects()
    {
        await using var factory = new PermissionWebFactory();
        var ids = await factory.SeedScopedProjectsAsync();
        var client = CreateClient(factory, RoleNames.ProjectStaff);

        var ownProject = await client.GetAsync($"/Workbench/Projects/Edit/{ids.OwnProjectId}");
        var otherProject = await client.GetAsync($"/Workbench/Projects/Edit/{ids.OtherProjectId}");
        var ownPlanning = await client.GetAsync($"/Workbench/PlanningProjects/Edit/{ids.OwnPlanningProjectId}");
        var otherPlanning = await client.GetAsync($"/Workbench/PlanningProjects/Edit/{ids.OtherPlanningProjectId}");
        var overview = await client.GetAsync("/Admin/Projects");

        ownProject.StatusCode.Should().Be(HttpStatusCode.OK);
        otherProject.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ownPlanning.StatusCode.Should().Be(HttpStatusCode.OK);
        otherPlanning.StatusCode.Should().Be(HttpStatusCode.NotFound);
        overview.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Project_staff_cannot_print_or_report_other_users_projects()
    {
        await using var factory = new PermissionWebFactory();
        var ids = await factory.SeedScopedProjectsAsync();
        var client = CreateClient(factory, RoleNames.ProjectStaff);

        var ganttPrint = await client.GetAsync($"/Workbench/Projects/GanttPrint?id={ids.OtherProjectId}");
        var planningPrint = await client.GetAsync($"/Workbench/PlanningProjects/Print?id={ids.OtherPlanningProjectId}");
        var planningPrintList = await client.GetAsync(
            $"/Workbench/PlanningProjects/PrintList?Ids={ids.OwnPlanningProjectId},{ids.OtherPlanningProjectId}");
        var report = await client.GetAsync("/Reports/OpenProjects");

        var planningPrintListHtml = WebUtility.HtmlDecode(await planningPrintList.Content.ReadAsStringAsync());
        var reportHtml = WebUtility.HtmlDecode(await report.Content.ReadAsStringAsync());

        ganttPrint.StatusCode.Should().Be(HttpStatusCode.NotFound);
        planningPrint.StatusCode.Should().Be(HttpStatusCode.NotFound);
        planningPrintList.StatusCode.Should().Be(HttpStatusCode.OK);
        planningPrintListHtml.Should().Contain("MY-SCOPED-PLANNING");
        planningPrintListHtml.Should().NotContain("OTHER-SCOPED-PLANNING");
        report.StatusCode.Should().Be(HttpStatusCode.OK);
        reportHtml.Should().Contain("MY-SCOPED-PROJECT");
        reportHtml.Should().NotContain("OTHER-SCOPED-PROJECT");
    }

    [Fact]
    public async Task Project_staff_cannot_open_manager_only_settlement_pages()
    {
        await using var factory = new PermissionWebFactory();
        var client = CreateClient(factory, RoleNames.ProjectStaff);

        var details = await client.GetAsync("/Settlements/Details/1");
        var print = await client.GetAsync("/Settlements/Print/1");

        details.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        print.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Regular_user_cannot_reassign_planning_project()
    {
        await using var factory = new PermissionWebFactory();
        var ids = await factory.SeedScopedProjectsAsync();
        var client = CreateClient(factory, RoleNames.ProjectStaff);
        var editResponse = await client.GetAsync($"/Workbench/PlanningProjects/Edit/{ids.OwnPlanningProjectId}");
        var editHtml = await editResponse.Content.ReadAsStringAsync();
        var token = ExtractAntiforgeryToken(editHtml);

        var response = await client.PostAsync(
            $"/Workbench/PlanningProjects/Edit/{ids.OwnPlanningProjectId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Input.Id"] = ids.OwnPlanningProjectId.ToString(),
                ["Input.Name"] = "UPDATED-MULTI-LEADER-PLANNING",
                ["Input.LeaderUserId"] = "other-user",
                ["Input.RecordYear"] = "2026",
                ["Input.RecordMonth"] = "7"
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await factory.GetPlanningLeaderUserIdAsync(ids.OwnPlanningProjectId))
            .Should().Be(TestAuthHandler.UserId);
    }

    [Fact]
    public async Task Last_active_system_administrator_cannot_be_demoted()
    {
        await using var factory = new PermissionWebFactory();
        var administrator = await factory.GetOnlyActiveAdministratorAsync();
        var client = CreateClient(factory, RoleNames.Administrator);
        var editResponse = await client.GetAsync($"/Admin/Users/Edit/{administrator.Id}");
        var token = ExtractAntiforgeryToken(await editResponse.Content.ReadAsStringAsync());

        var response = await client.PostAsync(
            $"/Admin/Users/Edit/{administrator.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Input.UserName"] = administrator.UserName ?? string.Empty,
                ["Input.DisplayName"] = administrator.DisplayName,
                ["Input.Email"] = administrator.Email ?? string.Empty,
                ["Input.IsActive"] = "true",
                ["Input.SelectedRoles"] = RoleNames.Leader
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await factory.IsInRoleAsync(administrator.Id, RoleNames.Administrator)).Should().BeTrue();
    }

    [Fact]
    public async Task Last_active_system_administrator_cannot_be_deleted()
    {
        await using var factory = new PermissionWebFactory();
        var administrator = await factory.GetOnlyActiveAdministratorAsync();
        var client = CreateClient(factory, RoleNames.Administrator);
        var indexResponse = await client.GetAsync("/Admin/Users");
        var token = ExtractAntiforgeryToken(await indexResponse.Content.ReadAsStringAsync());

        var response = await client.PostAsync(
            $"/Admin/Users?handler=Delete&id={administrator.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await factory.UserExistsAsync(administrator.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Batch_delete_cannot_remove_last_active_system_administrator()
    {
        await using var factory = new PermissionWebFactory();
        var administrator = await factory.GetOnlyActiveAdministratorAsync();
        var client = CreateClient(factory, RoleNames.Administrator);
        var indexResponse = await client.GetAsync("/Admin/Users");
        var token = ExtractAntiforgeryToken(await indexResponse.Content.ReadAsStringAsync());

        var response = await client.PostAsync(
            "/Admin/Users?handler=BatchDelete",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ids"] = administrator.Id
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await factory.UserExistsAsync(administrator.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Administrator_cannot_delete_the_current_account()
    {
        await using var factory = new PermissionWebFactory();
        await factory.CreateActiveAdministratorAsync(TestAuthHandler.UserId);
        var client = CreateClient(factory, RoleNames.Administrator);
        var indexResponse = await client.GetAsync("/Admin/Users");
        var token = ExtractAntiforgeryToken(await indexResponse.Content.ReadAsStringAsync());

        var response = await client.PostAsync(
            $"/Admin/Users?handler=Delete&id={TestAuthHandler.UserId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await factory.UserExistsAsync(TestAuthHandler.UserId)).Should().BeTrue();
    }

    [Fact]
    public async Task Referenced_user_delete_failure_is_shown_without_server_error()
    {
        await using var factory = new PermissionWebFactory();
        var userId = await factory.CreateReferencedUserAsync();
        var client = CreateClient(factory, RoleNames.Administrator);
        var indexResponse = await client.GetAsync("/Admin/Users");
        var token = ExtractAntiforgeryToken(await indexResponse.Content.ReadAsStringAsync());

        var response = await client.PostAsync(
            $"/Admin/Users?handler=Delete&id={userId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await factory.UserExistsAsync(userId)).Should().BeTrue();
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        match.Success.Should().BeTrue();
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static HttpClient CreateClient(PermissionWebFactory factory, string role)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        return client;
    }

    private sealed class PermissionWebFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("DataSource=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    connection.Open();
                }

                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddSingleton(connection);
                services.AddDbContext<ApplicationDbContext>((provider, options) =>
                    options.UseSqlite(provider.GetRequiredService<SqliteConnection>()));

                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                        options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { });
            });
        }

        public async Task SeedPlanningProjectsAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (await db.PlanningProjects.AnyAsync(x => x.Name == "MY-PLANNING-PROJECT"))
            {
                return;
            }

            db.Users.AddRange(
                new ApplicationUser
                {
                    Id = TestAuthHandler.UserId,
                    UserName = TestAuthHandler.UserId,
                    NormalizedUserName = TestAuthHandler.UserId.ToUpperInvariant(),
                    DisplayName = "Test User",
                    IsActive = true
                },
                new ApplicationUser
                {
                    Id = "other-user",
                    UserName = "other-user",
                    NormalizedUserName = "OTHER-USER",
                    DisplayName = "Other User",
                    IsActive = true
                },
                new ApplicationUser
                {
                    Id = "other-test-user",
                    UserName = "other-test-user",
                    NormalizedUserName = "OTHER-TEST-USER",
                    DisplayName = "Similar Id User",
                    IsActive = true
                });

            db.PlanningProjects.AddRange(
                new PlanningProject
                {
                    Name = "MY-PLANNING-PROJECT",
                    LeaderUserId = TestAuthHandler.UserId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                new PlanningProject
                {
                    Name = "OTHER-PLANNING-PROJECT",
                    LeaderUserId = "other-user",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                new PlanningProject
                {
                    Name = "COLLIDING-USER-ID-PLANNING-PROJECT",
                    LeaderUserId = "other-test-user",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            await db.SaveChangesAsync();
        }

        public async Task<ScopedProjectIds> SeedScopedProjectsAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await EnsureScopedUsersAsync(db);
            var statusId = await db.ProjectStatuses.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
            var now = DateTimeOffset.UtcNow;
            var ownProject = new Project
            {
                Year = 2026,
                ProjectNumber = "MY-SCOPED-PROJECT",
                Name = "MY-SCOPED-PROJECT",
                ProjectType = ProjectType.Engineering,
                StatusId = statusId,
                CreatedAt = now,
                UpdatedAt = now
            };
            ownProject.Assignments.Add(new ProjectAssignment
            {
                UserId = TestAuthHandler.UserId,
                RoleInProject = "專案人員"
            });
            var otherProject = new Project
            {
                Year = 2026,
                ProjectNumber = "OTHER-SCOPED-PROJECT",
                Name = "OTHER-SCOPED-PROJECT",
                ProjectType = ProjectType.Engineering,
                StatusId = statusId,
                CreatedAt = now,
                UpdatedAt = now
            };
            otherProject.Assignments.Add(new ProjectAssignment
            {
                UserId = "other-user",
                RoleInProject = "專案人員"
            });
            var ownPlanning = new PlanningProject
            {
                Name = "MY-SCOPED-PLANNING",
                LeaderUserId = TestAuthHandler.UserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            var otherPlanning = new PlanningProject
            {
                Name = "OTHER-SCOPED-PLANNING",
                LeaderUserId = "other-user",
                CreatedAt = now,
                UpdatedAt = now
            };

            db.Projects.AddRange(ownProject, otherProject);
            db.PlanningProjects.AddRange(ownPlanning, otherPlanning);
            await db.SaveChangesAsync();
            return new ScopedProjectIds(ownProject.Id, otherProject.Id, ownPlanning.Id, otherPlanning.Id);
        }

        public async Task<string?> GetPlanningLeaderUserIdAsync(int id)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.PlanningProjects
                .Where(x => x.Id == id)
                .Select(x => x.LeaderUserId)
                .SingleAsync();
        }

        public async Task<ApplicationUser> GetOnlyActiveAdministratorAsync()
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            var administrators = await userManager.GetUsersInRoleAsync(RoleNames.Administrator);
            return administrators.Single(x => x.IsActive);
        }

        public async Task<bool> IsInRoleAsync(string userId, string role)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("User not found.");
            return await userManager.IsInRoleAsync(user, role);
        }

        public async Task CreateActiveAdministratorAsync(string userId)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = userId,
                    UserName = userId,
                    DisplayName = "Current Administrator",
                    IsActive = true
                };
                (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();
            }

            if (!await userManager.IsInRoleAsync(user, RoleNames.Administrator))
            {
                (await userManager.AddToRoleAsync(user, RoleNames.Administrator)).Succeeded.Should().BeTrue();
            }
        }

        public async Task<bool> UserExistsAsync(string userId)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            return await userManager.FindByIdAsync(userId) is not null;
        }

        public async Task<string> CreateReferencedUserAsync()
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new ApplicationUser
            {
                Id = "referenced-user",
                UserName = "referenced-user",
                DisplayName = "Referenced User",
                IsActive = true
            };
            (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();
            db.PlanningProjects.Add(new PlanningProject
            {
                Name = "Referenced planning project",
                LeaderUserId = user.Id,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            return user.Id;
        }

        private static async Task EnsureScopedUsersAsync(ApplicationDbContext db)
        {
            if (await db.Users.AnyAsync(x => x.Id == TestAuthHandler.UserId))
            {
                return;
            }

            db.Users.AddRange(
                new ApplicationUser
                {
                    Id = TestAuthHandler.UserId,
                    UserName = TestAuthHandler.UserId,
                    NormalizedUserName = TestAuthHandler.UserId.ToUpperInvariant(),
                    DisplayName = "Test User",
                    IsActive = true
                },
                new ApplicationUser
                {
                    Id = "other-user",
                    UserName = "other-user",
                    NormalizedUserName = "OTHER-USER",
                    DisplayName = "Other User",
                    IsActive = true
                });
            await db.SaveChangesAsync();
        }

        public override async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    private sealed record ScopedProjectIds(
        int OwnProjectId,
        int OtherProjectId,
        int OwnPlanningProjectId,
        int OtherPlanningProjectId);

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string RoleHeader = "X-Test-Role";
        public const string UserId = "test-user";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers[RoleHeader].ToString();
            if (string.IsNullOrWhiteSpace(role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, UserId),
                    new Claim(ClaimTypes.Name, UserId),
                    new Claim(ClaimTypes.Role, role)
                ],
                SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
