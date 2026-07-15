using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using FluentAssertions;
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

public sealed class ChineseInterfaceSmokeTests
{
    [Fact]
    public async Task Anonymous_home_page_uses_chinese_application_chrome()
    {
        await using var factory = new ChineseInterfaceWebFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("專案管理系統");
        html.Should().Contain("請先登入");
        html.Should().Contain("登入");
        html.Should().NotContain(">Project Manager<");
        html.Should().NotContain(">Login<");
        html.Should().NotContain(">Register<");
        html.Should().NotContain("ASP.NET Core");
    }

    [Fact]
    public async Task Login_page_uses_chinese_labels()
    {
        await using var factory = new ChineseInterfaceWebFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/Identity/Account/Login");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("登入系統");
        html.Should().Contain("帳號");
        html.Should().Contain("密碼");
        html.Should().NotContain(">Email<");
        html.Should().NotContain(">Password<");
        html.Should().NotContain("Register as a new user");
    }

    [Fact]
    public async Task Error_page_uses_chinese_text()
    {
        await using var factory = new ChineseInterfaceWebFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/Error");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("系統錯誤");
        html.Should().Contain("請求編號");
        html.Should().NotContain("An error occurred while processing your request.");
        html.Should().NotContain("Development Mode");
    }

    [Fact]
    public async Task Administrator_user_page_uses_chinese_email_and_role_labels()
    {
        await using var factory = new ChineseInterfaceWebFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, RoleNames.Administrator);

        var response = await client.GetAsync("/Admin/Users");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("信箱");
        html.Should().Contain("系統管理員");
        html.Should().NotContain("<th>Email</th>");
        html.Should().NotContain(">Administrator<");
    }

    [Fact]
    public async Task Administrator_user_forms_show_four_primary_roles_without_legacy_viewer()
    {
        await using var factory = new ChineseInterfaceWebFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, RoleNames.Administrator);
        string administratorId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            administratorId = (await userManager.GetUsersInRoleAsync(RoleNames.Administrator)).Single().Id;
        }

        var create = await client.GetAsync("/Admin/Users/Create");
        var createHtml = WebUtility.HtmlDecode(await create.Content.ReadAsStringAsync());
        var edit = await client.GetAsync($"/Admin/Users/Edit/{administratorId}");
        var editHtml = WebUtility.HtmlDecode(await edit.Content.ReadAsStringAsync());

        create.StatusCode.Should().Be(HttpStatusCode.OK);
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        foreach (var html in new[] { createHtml, editHtml })
        {
            html.Should().Contain("系統管理員");
            html.Should().Contain("資訊管理員");
            html.Should().Contain("資料查看員");
            html.Should().Contain("一般使用者");
            html.Should().NotContain("舊查詢角色");
        }
    }

    [Fact]
    public void Project_root_contains_web_entry_index_file()
    {
        var root = FindRepositoryRoot();
        var indexPath = Path.Combine(root.FullName, "index.html");
        var launchSettingsPath = Path.Combine(
            root.FullName,
            "src",
            "ProjectManager.Web",
            "Properties",
            "launchSettings.json");

        File.Exists(indexPath).Should().BeTrue("專案資料夾根目錄需要一個 WEB 端入口檔案");
        File.Exists(launchSettingsPath).Should().BeTrue("入口檔案必須和 ASP.NET Core 啟動位址保持一致");

        var html = File.ReadAllText(indexPath);
        var localHttpUrl = GetHttpApplicationUrl(launchSettingsPath);

        html.Should().Contain("專案管理系統 WEB 入口");
        html.Should().Contain("entry-brand-mark");
        html.Should().Contain($"{localHttpUrl}/Workbench/Projects");
        html.Should().Contain($"{localHttpUrl}/Identity/Account/Login");
    }

    private static string GetHttpApplicationUrl(string launchSettingsPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(launchSettingsPath));
        var profiles = document.RootElement.GetProperty("profiles");
        var httpProfile = profiles.GetProperty("http");
        var applicationUrl = httpProfile.GetProperty("applicationUrl").GetString()
            ?? throw new InvalidOperationException("Missing http applicationUrl.");

        return applicationUrl
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Single(x => x.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            .TrimEnd('/');
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProjectManager.sln")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new DirectoryNotFoundException("Cannot locate ProjectManager.sln.");
    }

    private sealed class ChineseInterfaceWebFactory : WebApplicationFactory<Program>
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
