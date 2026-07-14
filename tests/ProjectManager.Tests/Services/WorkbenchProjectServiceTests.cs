using FluentAssertions;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class WorkbenchProjectServiceTests
{
    [Fact]
    public async Task GetProjectsForUserAsync_returns_assigned_projects_for_project_staff()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var seed = await SeedWorkbenchProjectsAsync(db);
        var service = new WorkbenchProjectService(db, new AuditLogService(db));

        var projects = await service.GetProjectsForUserAsync(seed.StaffUserId, canViewAll: false, CancellationToken.None);

        projects.Should().ContainSingle();
        projects[0].ProjectNumber.Should().Be("P-ASSIGNED");
    }

    [Fact]
    public async Task GetProjectsForUserAsync_returns_all_non_deleted_projects_for_leader()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var seed = await SeedWorkbenchProjectsAsync(db);
        var service = new WorkbenchProjectService(db, new AuditLogService(db));

        var projects = await service.GetProjectsForUserAsync(seed.LeaderUserId, canViewAll: true, CancellationToken.None);

        projects.Select(x => x.ProjectNumber).Should().BeEquivalentTo("P-ASSIGNED", "P-OTHER");
    }

    [Theory]
    [InlineData(ProjectAnalysisTypes.Overdue, "P-ASSIGNED")]
    [InlineData(ProjectAnalysisTypes.Pending, "P-OTHER")]
    [InlineData(ProjectAnalysisTypes.Upcoming, "P-OTHER")]
    public async Task GetProjectsForUserPageAsync_applies_personal_workbench_analysis_filter(
        string analysisType,
        string expectedProjectNumber)
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var seed = await SeedWorkbenchProjectsAsync(db);
        var service = new WorkbenchProjectService(db, new AuditLogService(db));

        var page = await service.GetProjectsForUserPageAsync(
            seed.LeaderUserId,
            canViewAll: true,
            new ProjectFilter(null, null, null, null, null, null, true, analysisType),
            pageNumber: 1,
            pageSize: 20,
            CancellationToken.None);

        page.Items.Select(x => x.ProjectNumber).Should().Equal(expectedProjectNumber);
    }

    [Fact]
    public async Task UpdateProgressAsync_updates_progress_fields_only_for_assigned_project_staff()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var seed = await SeedWorkbenchProjectsAsync(db);
        var service = new WorkbenchProjectService(db, new AuditLogService(db));

        var result = await service.UpdateProgressAsync(
            new UpdateProgressRequest(
                seed.AssignedProjectId,
                seed.StaffUserId,
                CanEditAll: false,
                ProgressPercent: 65,
                ProgressDescription: "现场调试完成"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        var project = db.Projects.Single(x => x.Id == seed.AssignedProjectId);
        project.ProgressPercent.Should().Be(65);
        project.ProgressDescription.Should().Be("现场调试完成");
        project.ProjectAmount.Should().Be(10000);
        project.CollectionPercent.Should().Be(20);
        db.PurchaseRequests.Single(x => x.ProjectId == seed.AssignedProjectId).PurchaseAmount.Should().Be(1500);
    }

    [Fact]
    public async Task UpdateProgressAsync_writes_project_audit_log()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var seed = await SeedWorkbenchProjectsAsync(db);
        var service = new WorkbenchProjectService(db, new AuditLogService(db));

        var result = await service.UpdateProgressAsync(
            new UpdateProgressRequest(
                seed.AssignedProjectId,
                seed.StaffUserId,
                CanEditAll: false,
                ProgressPercent: 65,
                ProgressDescription: "现场调试完成"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        var log = db.AuditLogs.Single();
        log.UserId.Should().Be(seed.StaffUserId);
        log.Action.Should().Be("ProgressUpdate");
        log.ProjectId.Should().Be(seed.AssignedProjectId);
        log.ProjectNumber.Should().Be("P-ASSIGNED");
        log.ChangeSummary.Should().Contain("更新進度");
        log.ChangeDetailsJson.Should().Contain("專案進度");
        log.ChangeDetailsJson.Should().Contain("30%");
        log.ChangeDetailsJson.Should().Contain("65%");
    }

    [Fact]
    public async Task UpdateProgressAsync_rejects_unassigned_project_staff()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var seed = await SeedWorkbenchProjectsAsync(db);
        var service = new WorkbenchProjectService(db, new AuditLogService(db));

        var result = await service.UpdateProgressAsync(
            new UpdateProgressRequest(
                seed.OtherProjectId,
                seed.StaffUserId,
                CanEditAll: false,
                ProgressPercent: 80,
                ProgressDescription: "不应更新"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("Project is not assigned to the current user.");
        db.Projects.Single(x => x.Id == seed.OtherProjectId).ProgressPercent.Should().Be(10);
    }

    [Fact]
    public async Task UpdateProgressAsync_rejects_stale_row_version_without_overwriting_newer_data()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var seed = await SeedWorkbenchProjectsAsync(db);
        var service = new WorkbenchProjectService(db, new AuditLogService(db));

        var result = await service.UpdateProgressAsync(
            new UpdateProgressRequest(
                seed.AssignedProjectId,
                seed.StaffUserId,
                CanEditAll: false,
                ProgressPercent: 99,
                ProgressDescription: "過期畫面送出的內容",
                RowVersion: Convert.ToBase64String(new byte[8])),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.IsConcurrencyConflict.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Contain("已被其他使用者更新");

        db.ChangeTracker.Clear();
        var project = db.Projects.Single(x => x.Id == seed.AssignedProjectId);
        project.ProgressPercent.Should().Be(30);
    }

    private static async Task<SeedIds> SeedWorkbenchProjectsAsync(ProjectManager.Web.Data.ApplicationDbContext db)
    {
        var staff = new ApplicationUser
        {
            Id = "staff-1",
            UserName = "staff",
            DisplayName = "项目人员"
        };
        var leader = new ApplicationUser
        {
            Id = "leader-1",
            UserName = "leader",
            DisplayName = "领导"
        };
        var status = new ProjectStatus
        {
            Code = "Created",
            Name = "已立案",
            SortOrder = 10,
            IsClosed = false
        };
        var blockedStatus = new ProjectStatus
        {
            Code = "Blocked",
            Name = "阻塞",
            SortOrder = 20,
            IsClosed = false
        };
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var assignedProject = new Project
        {
            Year = 2026,
            ParentCaseNumber = "M-001",
            ProjectNumber = "P-ASSIGNED",
            Name = "Assigned Project",
            Status = status,
            ProjectAmount = 10000,
            ProgressPercent = 30,
            CollectionPercent = 20,
            GanttPlan = new ProjectGanttPlan
            {
                StartDate = today.AddDays(-30),
                FinishDate = today.AddDays(-1)
            },
            UpdatedByUser = staff,
            Assignments =
            {
                new ProjectAssignment
                {
                    User = staff,
                    RoleInProject = "专案人员"
                }
            },
            PurchaseRequests =
            {
                new PurchaseRequest
                {
                    RequestNumber = "PR-001",
                    PurchaseType = PurchaseType.InternalPurchase,
                    PurchaseAmount = 1500,
                    PaymentPercent = 50,
                    ActualPaidAmount = 700
                }
            }
        };
        var otherProject = new Project
        {
            Year = 2026,
            ParentCaseNumber = "M-002",
            ProjectNumber = "P-OTHER",
            Name = "Other Project",
            Status = blockedStatus,
            ProjectAmount = 20000,
            ProgressPercent = 10,
            CollectionPercent = 0,
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-45),
            GanttPlan = new ProjectGanttPlan
            {
                StartDate = today,
                FinishDate = today.AddDays(30),
                Tasks =
                {
                    new ProjectGanttTask
                    {
                        SortOrder = 10,
                        Name = "近期節點",
                        PlannedStartDate = today,
                        PlannedFinishDate = today.AddDays(7),
                        ProgressPercent = 20
                    }
                }
            }
        };
        var deletedProject = new Project
        {
            Year = 2026,
            ProjectNumber = "P-DELETED",
            Name = "Deleted Project",
            Status = status,
            IsDeleted = true
        };

        db.Users.AddRange(staff, leader);
        db.Projects.AddRange(assignedProject, otherProject, deletedProject);
        await db.SaveChangesAsync();

        return new SeedIds(staff.Id, leader.Id, assignedProject.Id, otherProject.Id);
    }

    private sealed record SeedIds(
        string StaffUserId,
        string LeaderUserId,
        int AssignedProjectId,
        int OtherProjectId);
}
