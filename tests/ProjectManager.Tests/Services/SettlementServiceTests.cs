using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class SettlementServiceTests
{
    [Fact]
    public async Task CreateAsync_allows_multiple_batches_for_same_month()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var ids = await SeedSettlementProjectsAsync(db);
        var service = new SettlementService(db);

        var first = await service.CreateAsync(
            new CreateSettlementRequest(2026, 7, ids.AdminUserId, "first"),
            CancellationToken.None);
        var second = await service.CreateAsync(
            new CreateSettlementRequest(2026, 7, ids.AdminUserId, "second"),
            CancellationToken.None);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        var batches = await db.MonthlySettlementBatches
            .OrderBy(x => x.BatchNumber)
            .ToListAsync();
        batches.Select(x => x.BatchNumber).Should().Equal(1, 2);
    }

    [Fact]
    public async Task CreateAsync_captures_snapshot_fields_and_excludes_soft_deleted_projects()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var ids = await SeedSettlementProjectsAsync(db);
        var service = new SettlementService(db);

        var result = await service.CreateAsync(
            new CreateSettlementRequest(2026, 7, ids.AdminUserId, "snapshot"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        var batch = await db.MonthlySettlementBatches
            .Include(x => x.Items)
            .SingleAsync(x => x.Id == result.BatchId);

        batch.Items.Should().ContainSingle();
        var item = batch.Items.Single();
        item.ParentCaseNumber.Should().Be("M-001");
        item.ProjectNumber.Should().Be("P-001");
        item.ClosedYearMonth.Should().Be(new DateOnly(2026, 7, 1));
        item.PurchaseRequestSummary.Should().Be("PR-001; PR-002");
        item.SubCaseContactSummary.Should().Be("Contact User");
        item.PurchaseAmountTotal.Should().Be(3000);
        item.ActualPaidAmountTotal.Should().Be(1500);
        item.ProgressDescription.Should().Be("Ready for monthly settlement");
        item.UpdatedByUserName.Should().Be("Staff User");
        item.SourceUpdatedAt.Should().Be(new DateTimeOffset(2026, 7, 10, 8, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task CreateAsync_rejects_invalid_month()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var ids = await SeedSettlementProjectsAsync(db);
        var service = new SettlementService(db);

        var result = await service.CreateAsync(
            new CreateSettlementRequest(2026, 13, ids.AdminUserId, null),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.BatchId.Should().BeNull();
        result.Errors.Should().Contain("Settlement month must be between 1 and 12.");
    }

    private static async Task<SeedIds> SeedSettlementProjectsAsync(ProjectManager.Web.Data.ApplicationDbContext db)
    {
        var admin = new ApplicationUser
        {
            Id = "admin-1",
            UserName = "admin",
            DisplayName = "Admin User"
        };
        var staff = new ApplicationUser
        {
            Id = "staff-1",
            UserName = "staff",
            DisplayName = "Staff User"
        };
        var contact = new ApplicationUser
        {
            Id = "contact-1",
            UserName = "contact",
            DisplayName = "Contact User"
        };
        var closedStatus = new ProjectStatus
        {
            Code = "Closed",
            Name = "已结案",
            SortOrder = 60,
            IsClosed = true
        };

        var project = new Project
        {
            Year = 2026,
            ParentCaseNumber = "M-001",
            ProjectNumber = "P-001",
            Name = "Settlement Project",
            Status = closedStatus,
            ClosedYearMonth = new DateOnly(2026, 7, 1),
            ProjectAmount = 10000,
            ProgressPercent = 100,
            CollectionPercent = 80,
            ProgressDescription = "Ready for monthly settlement",
            UpdatedByUser = staff,
            UpdatedAt = new DateTimeOffset(2026, 7, 10, 8, 30, 0, TimeSpan.Zero),
            Assignments =
            {
                new ProjectAssignment
                {
                    User = staff,
                    RoleInProject = "Owner"
                }
            },
            PurchaseRequests =
            {
                new PurchaseRequest
                {
                    RequestNumber = "PR-001",
                    PurchaseType = PurchaseType.InternalPurchase,
                    PurchaseStaff = staff,
                    SubCaseContact = contact,
                    PurchaseAmount = 1000,
                    PaymentPercent = 50,
                    ActualPaidAmount = 500
                },
                new PurchaseRequest
                {
                    RequestNumber = "PR-002",
                    PurchaseType = PurchaseType.ExternalPurchase,
                    PurchaseStaff = staff,
                    SubCaseContact = contact,
                    PurchaseAmount = 2000,
                    PaymentPercent = 50,
                    ActualPaidAmount = 1000
                }
            }
        };

        var deletedProject = new Project
        {
            Year = 2026,
            ProjectNumber = "P-DELETED",
            Name = "Deleted Project",
            Status = closedStatus,
            ProjectAmount = 999,
            ProgressPercent = 100,
            CollectionPercent = 100,
            UpdatedByUser = staff,
            IsDeleted = true
        };

        db.Users.AddRange(admin, staff, contact);
        db.Projects.AddRange(project, deletedProject);
        await db.SaveChangesAsync();

        return new SeedIds(admin.Id);
    }

    private sealed record SeedIds(string AdminUserId);
}
