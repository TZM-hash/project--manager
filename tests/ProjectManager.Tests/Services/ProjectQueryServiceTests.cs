using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class ProjectQueryServiceTests
{
    [Fact]
    public async Task GetProjectsAsync_excludes_closed_projects_when_open_only()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        await SeedProjectsAsync(db);
        var service = new ProjectQueryService(db);

        var projects = await service.GetProjectsAsync(
            new ProjectFilter(null, null, null, null, null, null, OpenOnly: true),
            CancellationToken.None);

        projects.Should().ContainSingle();
        projects[0].ProjectNumber.Should().Be("P-OPEN");
    }

    [Fact]
    public async Task GetProjectsAsync_applies_parent_case_personnel_and_status_filters()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var ids = await SeedProjectsAsync(db);
        var service = new ProjectQueryService(db);

        var projects = await service.GetProjectsAsync(
            new ProjectFilter(
                Year: 2026,
                ParentCaseNumber: "M-001",
                ProjectNumber: "OPEN",
                ProjectName: "Open",
                PersonnelUserId: ids.StaffUserId,
                StatusId: ids.OpenStatusId,
                OpenOnly: true),
            CancellationToken.None);

        projects.Should().ContainSingle();
        projects[0].ParentCaseNumber.Should().Be("M-001");
    }

    [Fact]
    public async Task GetOpenProjectSummaryAsync_groups_open_projects_by_status_and_totals_amounts()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        await SeedProjectsAsync(db);
        var service = new ProjectQueryService(db);

        var summary = await service.GetOpenProjectSummaryAsync(
            new ProjectFilter(2026, null, null, null, null, null, OpenOnly: true),
            CancellationToken.None);

        summary.Should().ContainSingle();
        summary[0].StatusName.Should().Be("已立案");
        summary[0].Count.Should().Be(1);
        summary[0].ProjectAmountTotal.Should().Be(10000);
        summary[0].PurchaseAmountTotal.Should().Be(1500);
        summary[0].ActualPaidAmountTotal.Should().Be(750);
    }

    private static async Task<SeedIds> SeedProjectsAsync(ProjectManager.Web.Data.ApplicationDbContext db)
    {
        var staff = new ApplicationUser
        {
            Id = "staff-1",
            UserName = "staff",
            DisplayName = "Staff User"
        };
        var openStatus = new ProjectStatus
        {
            Code = "Created",
            Name = "已立案",
            SortOrder = 10,
            IsClosed = false
        };
        var closedStatus = new ProjectStatus
        {
            Code = "Closed",
            Name = "已结案",
            SortOrder = 60,
            IsClosed = true
        };

        var openProject = new Project
        {
            Year = 2026,
            ParentCaseNumber = "M-001",
            ProjectNumber = "P-OPEN",
            Name = "Open Project",
            Status = openStatus,
            ProjectAmount = 10000,
            ProgressPercent = 30,
            CollectionPercent = 20,
            UpdatedByUser = staff,
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
                    SubCaseContact = staff,
                    PurchaseAmount = 1500,
                    PaymentPercent = 50,
                    ActualPaidAmount = 750
                }
            }
        };

        var closedProject = new Project
        {
            Year = 2026,
            ParentCaseNumber = "M-002",
            ProjectNumber = "P-CLOSED",
            Name = "Closed Project",
            Status = closedStatus,
            ClosedYearMonth = new DateOnly(2026, 6, 1),
            ProjectAmount = 20000,
            ProgressPercent = 100,
            CollectionPercent = 100,
            UpdatedByUser = staff
        };

        db.Projects.AddRange(openProject, closedProject);
        await db.SaveChangesAsync();

        return new SeedIds(staff.Id, openStatus.Id);
    }

    private sealed record SeedIds(string StaffUserId, int OpenStatusId);
}
