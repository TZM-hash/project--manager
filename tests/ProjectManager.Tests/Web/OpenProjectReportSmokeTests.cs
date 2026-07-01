using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
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

public sealed class OpenProjectReportSmokeTests
{
    [Fact]
    public async Task Leader_can_access_open_project_report()
    {
        await using var factory = new ReportWebFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, RoleNames.Leader);
        await factory.SeedProjectsAsync();

        var response = await client.GetAsync("/Reports/OpenProjects");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Viewer_can_access_open_project_report()
    {
        await using var factory = new ReportWebFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, RoleNames.Viewer);
        await factory.SeedProjectsAsync();

        var response = await client.GetAsync("/Reports/OpenProjects");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProjectStaff_sees_only_assigned_open_projects()
    {
        await using var factory = new ReportWebFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, RoleNames.ProjectStaff);
        await factory.SeedProjectsAsync();

        var response = await client.GetAsync("/Reports/OpenProjects");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Assigned Project");
        html.Should().NotContain("Other Project");
    }

    [Fact]
    public async Task Export_action_returns_open_project_excel_content_type()
    {
        await using var factory = new ReportWebFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, RoleNames.Leader);
        await factory.SeedProjectsAsync();

        var response = await client.GetAsync("/Reports/OpenProjects?handler=Export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private sealed class ReportWebFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("DataSource=:memory:");

        public async Task SeedProjectsAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (db.Projects.Any())
            {
                return;
            }

            var staff = new ApplicationUser
            {
                Id = "test-user",
                UserName = "staff",
                DisplayName = "项目人员"
            };
            var status = db.ProjectStatuses.Single(x => x.Code == "Created");
            var closed = db.ProjectStatuses.Single(x => x.Code == "Closed");

            db.Users.Add(staff);
            db.Projects.AddRange(
                new Project
                {
                    Year = 2026,
                    ParentCaseNumber = "M-001",
                    ProjectNumber = "P-ASSIGNED",
                    Name = "Assigned Project",
                    Status = status,
                    ProjectAmount = 10000,
                    ProgressPercent = 30,
                    CollectionPercent = 20,
                    Assignments =
                    {
                        new ProjectAssignment
                        {
                            User = staff,
                            RoleInProject = "专案人员"
                        }
                    }
                },
                new Project
                {
                    Year = 2026,
                    ParentCaseNumber = "M-002",
                    ProjectNumber = "P-OTHER",
                    Name = "Other Project",
                    Status = status,
                    ProjectAmount = 20000,
                    ProgressPercent = 10,
                    CollectionPercent = 0
                },
                new Project
                {
                    Year = 2026,
                    ProjectNumber = "P-CLOSED",
                    Name = "Closed Project",
                    Status = closed,
                    ProjectAmount = 5000,
                    ProgressPercent = 100,
                    CollectionPercent = 100
                });

            await db.SaveChangesAsync();
        }

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

        public override async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string RoleHeader = "X-Test-Role";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers[RoleHeader].ToString();
            if (string.IsNullOrWhiteSpace(role))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "test-user"),
                new(ClaimTypes.Name, "test-user"),
                new(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
