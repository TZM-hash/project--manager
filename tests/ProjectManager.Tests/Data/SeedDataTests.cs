using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;

namespace ProjectManager.Tests.Data;

public sealed class SeedDataTests
{
    [Fact]
    public void Initial_statuses_include_closed_status_with_red_bold_style()
    {
        var closed = SeedData.InitialStatuses.Single(x => x.Code == "Closed");
        var style = SeedData.InitialStatusStyles.Single(x => x.StatusCode == "Closed");

        closed.Name.Should().Be("已結案");
        closed.IsClosed.Should().BeTrue();
        style.TextColor.Should().Be("#dc2626");
        style.IsBold.Should().BeTrue();
    }

    [Fact]
    public void Role_names_include_required_initial_roles()
    {
        RoleNames.All.Should().BeEquivalentTo(
            RoleNames.Administrator,
            RoleNames.ProjectStaff,
            RoleNames.Leader,
            RoleNames.DataViewer,
            RoleNames.SubCaseContact);
    }

    [Fact]
    public async Task Seeding_migrates_legacy_viewer_to_project_staff_and_deletes_legacy_role()
    {
        await using var factory = new SeedDataWebFactory();
        _ = factory.Services;

        string userId;
        using (var scope = factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            (await roleManager.CreateAsync(new IdentityRole(RoleNames.LegacyViewer))).Succeeded.Should().BeTrue();
            var user = new ApplicationUser
            {
                UserName = "legacy-viewer",
                DisplayName = "Legacy Viewer",
                IsActive = true
            };
            (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();
            (await userManager.AddToRoleAsync(user, RoleNames.LegacyViewer)).Succeeded.Should().BeTrue();
            userId = user.Id;
        }

        await SeedData.EnsureSeededAsync(factory.Services);

        using (var scope = factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);

            user.Should().NotBeNull();
            (await userManager.IsInRoleAsync(user!, RoleNames.ProjectStaff)).Should().BeTrue();
            (await userManager.IsInRoleAsync(user!, RoleNames.LegacyViewer)).Should().BeFalse();
            (await roleManager.RoleExistsAsync(RoleNames.LegacyViewer)).Should().BeFalse();
        }
    }

    private sealed class SeedDataWebFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("DataSource=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                connection.Open();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddSingleton(connection);
                services.AddDbContext<ApplicationDbContext>((provider, options) =>
                    options.UseSqlite(provider.GetRequiredService<SqliteConnection>()));
            });
        }

        public override async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
