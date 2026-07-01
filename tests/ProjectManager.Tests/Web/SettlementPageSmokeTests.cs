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

public sealed class SettlementPageSmokeTests
{
    [Fact]
    public async Task Administrator_can_open_settlement_creation_page()
    {
        await using var factory = new SettlementWebFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, RoleNames.Administrator);

        var response = await client.GetAsync("/Settlements/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProjectStaff_cannot_create_settlement()
    {
        await using var factory = new SettlementWebFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, RoleNames.ProjectStaff);

        var response = await client.GetAsync("/Settlements/Create");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Leader_can_view_settlement_details_and_print_page()
    {
        await using var factory = new SettlementWebFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, RoleNames.Leader);
        var batchId = await factory.SeedSettlementBatchAsync();

        var details = await client.GetAsync($"/Settlements/Details/{batchId}");
        var print = await client.GetAsync($"/Settlements/Print/{batchId}");

        details.StatusCode.Should().Be(HttpStatusCode.OK);
        print.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Export_action_returns_excel_content_type()
    {
        await using var factory = new SettlementWebFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, RoleNames.Leader);
        var batchId = await factory.SeedSettlementBatchAsync();

        var response = await client.GetAsync($"/Settlements/Details/{batchId}?handler=Export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private sealed class SettlementWebFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("DataSource=:memory:");

        public async Task<int> SeedSettlementBatchAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new ApplicationUser
            {
                Id = "creator-1",
                UserName = "creator",
                DisplayName = "Creator"
            };
            if (!db.Users.Any(x => x.Id == user.Id))
            {
                db.Users.Add(user);
            }

            var batch = new MonthlySettlementBatch
            {
                Year = 2026,
                Month = 7,
                BatchNumber = 1,
                CreatedByUserId = user.Id,
                Notes = "Smoke"
            };
            batch.Items.Add(new MonthlySettlementItem
            {
                ProjectId = 1,
                ParentCaseNumber = "M-001",
                ProjectNumber = "P-001",
                ProjectName = "Settlement Project",
                ProjectPersonnelText = "Creator",
                ProgressPercent = 50,
                ProjectAmount = 10000,
                CollectionPercent = 20,
                StatusName = "已立案",
                PurchaseRequestSummary = "PR-001",
                PurchaseAmountTotal = 1000,
                SubCaseContactSummary = "Creator",
                PaymentPercentSummary = "50%",
                ActualPaidAmountTotal = 500,
                UpdatedByUserName = "Creator",
                SourceUpdatedAt = DateTimeOffset.UtcNow
            });
            db.MonthlySettlementBatches.Add(batch);
            await db.SaveChangesAsync();
            return batch.Id;
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
