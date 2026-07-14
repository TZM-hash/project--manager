using FluentAssertions;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services.Workbench;

namespace ProjectManager.Tests.Services;

public sealed class PersonalWorkbenchServiceTests
{
    [Fact]
    public async Task Project_staff_only_sees_assigned_risk_items()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var today = new DateOnly(2026, 7, 15);
        var status = Status("Created", "已立案", 10);
        db.ProjectStatuses.Add(status);
        db.Users.AddRange(User("staff"), User("other"));
        db.Projects.AddRange(
            Project("P-MINE", status, today.AddDays(-5), "staff"),
            Project("P-OTHER", status, today.AddDays(-8), "other"));
        await db.SaveChangesAsync();
        var service = new PersonalWorkbenchService(db, new FixedTimeProvider(today));

        var snapshot = await service.BuildAsync("staff", canViewAll: false, CancellationToken.None);

        snapshot.OverdueCount.Should().Be(1);
        snapshot.OverdueProjects.Should().ContainSingle(x => x.ProjectNumber == "P-MINE");
        snapshot.OverdueProjects.Should().NotContain(x => x.ProjectNumber == "P-OTHER");
    }

    [Fact]
    public async Task Snapshot_prioritizes_overdue_then_pending_then_upcoming_then_stale()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var today = new DateOnly(2026, 7, 15);
        var created = Status("Created", "已立案", 10);
        var waiting = Status("Waiting", "等待處理", 90);
        db.ProjectStatuses.AddRange(created, waiting);
        db.Projects.AddRange(
            Project("P-OVERDUE", created, today.AddDays(-1)),
            Project("P-PENDING", waiting, today.AddDays(20)),
            Project("P-UPCOMING", created, today.AddDays(10), taskFinish: today.AddDays(7)),
            Project("P-STALE", created, today.AddDays(30), updatedAt: today.AddDays(-40)));
        await db.SaveChangesAsync();
        var service = new PersonalWorkbenchService(db, new FixedTimeProvider(today));

        var snapshot = await service.BuildAsync("admin", canViewAll: true, CancellationToken.None);

        snapshot.OverdueCount.Should().Be(1);
        snapshot.PendingCount.Should().Be(1);
        snapshot.UpcomingNodeCount.Should().Be(1);
        snapshot.StaleCount.Should().Be(1);
        snapshot.HeroTone.Should().Be("danger");
        snapshot.HeroTitle.Should().Contain("1").And.Contain("逾期");
        snapshot.PrimaryActionUrl.Should().Contain("overdue");
    }

    [Fact]
    public async Task Empty_snapshot_points_to_my_projects()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var service = new PersonalWorkbenchService(db, new FixedTimeProvider(new DateOnly(2026, 7, 15)));

        var snapshot = await service.BuildAsync("admin", canViewAll: true, CancellationToken.None);

        snapshot.HeroTone.Should().Be("success");
        snapshot.HeroTitle.Should().Contain("沒有高優先風險");
        snapshot.PrimaryActionUrl.Should().Be("/Workbench/Projects/Index");
    }

    private static Project Project(
        string number,
        ProjectStatus status,
        DateOnly planFinish,
        string? assigneeId = null,
        DateOnly? taskFinish = null,
        DateOnly? updatedAt = null)
    {
        var project = new Project
        {
            Year = 2026,
            ProjectNumber = number,
            Name = number,
            Status = status,
            ProgressPercent = 50,
            CollectionPercent = 50,
            UpdatedAt = new DateTimeOffset((updatedAt ?? new DateOnly(2026, 7, 14)).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            GanttPlan = new ProjectGanttPlan
            {
                FinishDate = planFinish,
                Tasks =
                {
                    new ProjectGanttTask
                    {
                        SortOrder = 1,
                        Name = $"{number} 節點",
                        PlannedStartDate = taskFinish?.AddDays(-2),
                        PlannedFinishDate = taskFinish,
                        ProgressPercent = taskFinish.HasValue ? 30 : 0
                    }
                }
            }
        };
        if (assigneeId is not null)
        {
            project.Assignments.Add(new ProjectAssignment { UserId = assigneeId, RoleInProject = "ProjectStaff" });
        }
        return project;
    }

    private static ApplicationUser User(string id) => new()
    {
        Id = id,
        UserName = id,
        DisplayName = id,
        IsActive = true
    };

    private static ProjectStatus Status(string code, string name, int sortOrder) => new()
    {
        Code = code,
        Name = name,
        SortOrder = sortOrder,
        IsActive = true
    };

    private sealed class FixedTimeProvider(DateOnly today) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }
}
