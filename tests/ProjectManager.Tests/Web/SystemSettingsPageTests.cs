using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
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
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Web;

public sealed class SystemSettingsPageTests
{
    [Fact]
    public async Task Administrator_can_access_system_settings_page()
    {
        await using var factory = new ProjectManagerWebFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, RoleNames.Administrator);

        var response = await client.GetAsync("/Admin/Settings");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("name=\"VisualTheme\"");
        html.Should().Contain("value=\"Default\"");
        html.Should().Contain("value=\"ClearGlass\"");
        html.Should().Contain("name=\"UiEffectsLevel\"");
        html.Should().Contain("value=\"Low\"");
        html.Should().Contain("value=\"Medium\"");
        html.Should().Contain("value=\"High\"");
        html.Should().Contain("name=\"ArchiveDate\"");
    }

    [Fact]
    public async Task Home_page_applies_saved_ui_effects_level_class()
    {
        await using var factory = new ProjectManagerWebFactory();
        await factory.SetUiEffectsLevelAsync(UiEffectsLevel.High);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("<body class=\"app-shell theme-default ui-effects-high\">");
    }

    [Fact]
    public async Task Home_page_applies_saved_clear_glass_theme_class()
    {
        await using var factory = new ProjectManagerWebFactory();
        await factory.SetVisualThemeAsync(SystemSettingsService.VisualTheme.ClearGlass);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("<body class=\"app-shell theme-clear-glass ui-effects-medium\">");
    }

    private sealed class ProjectManagerWebFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("DataSource=:memory:");

        public async Task SetUiEffectsLevelAsync(UiEffectsLevel level)
        {
            using var scope = Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<SystemSettingsService>();
            await service.SetUiEffectsLevelAsync(level, CancellationToken.None);
        }

        public async Task SetVisualThemeAsync(SystemSettingsService.VisualTheme theme)
        {
            using var scope = Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<SystemSettingsService>();
            await service.SetVisualThemeAsync(theme, CancellationToken.None);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
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
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
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
