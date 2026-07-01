using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
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
        html.Should().Contain("项目管理系统");
        html.Should().Contain("请先登录");
        html.Should().Contain("登录");
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
        html.Should().Contain("登录系统");
        html.Should().Contain("账号");
        html.Should().Contain("密码");
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
        html.Should().Contain("系统错误");
        html.Should().Contain("请求编号");
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
        html.Should().Contain("邮箱");
        html.Should().Contain("系统管理员");
        html.Should().NotContain("<th>Email</th>");
        html.Should().NotContain(">Administrator<");
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

        File.Exists(indexPath).Should().BeTrue("项目文件夹根目录需要一个 WEB 端入口文件");
        File.Exists(launchSettingsPath).Should().BeTrue("入口文件必须和 ASP.NET Core 启动地址保持一致");

        var html = File.ReadAllText(indexPath);
        var localHttpUrl = GetHttpApplicationUrl(launchSettingsPath);

        html.Should().Contain("项目管理系统 WEB 入口");
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
