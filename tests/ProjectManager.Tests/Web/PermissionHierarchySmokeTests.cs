using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
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

        attribute.Roles.Should().Be(RoleNames.BusinessManagerRoles);
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

    [Theory]
    [InlineData(RoleNames.ProjectStaff)]
    [InlineData(RoleNames.Viewer)]
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
    public async Task Legacy_regular_user_cannot_print_or_report_other_users_projects()
    {
        await using var factory = new PermissionWebFactory();
        var ids = await factory.SeedScopedProjectsAsync();
        var client = CreateClient(factory, RoleNames.Viewer);

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
    public async Task Legacy_regular_user_cannot_open_manager_only_settlement_pages()
    {
        await using var factory = new PermissionWebFactory();
        var client = CreateClient(factory, RoleNames.Viewer);

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
