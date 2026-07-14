using FluentAssertions;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class ProjectGanttServiceTests
{
    [Fact]
    public async Task BuildInputAsync_CreatesReferenceTemplateForProjectWithoutPlan()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var _ = connection;
        await using var __ = db;
        var status = new ProjectStatus { Code = "doing", Name = "執行中", SortOrder = 1 };
        var project = new Project
        {
            Year = 2026,
            ProjectNumber = "M6B22",
            Name = "自動倉儲項目",
            Status = status,
            ProgressPercent = 30m
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var input = await new ProjectGanttService(db, new SystemSettingsService(db))
            .BuildInputAsync(project.Id, CancellationToken.None);

        input.Tasks.Should().HaveCount(10);
        input.Tasks.Select(x => x.SortOrder).Should().Equal(Enumerable.Range(1, 10));
        ProjectGanttService.CalculateActualProgress(input.Tasks, ProjectGanttService.BuildTaskWeights(10))
            .Should().Be(30m);
    }

    [Fact]
    public void BuildMonths_AdaptsToMonthlyBucketsForMediumProject()
    {
        var input = new ProjectGanttInputModel
        {
            StartDate = new DateOnly(2026, 4, 1),
            FinishDate = new DateOnly(2026, 10, 31)
        };

        var months = ProjectGanttService.BuildMonths(input, new DateOnly(2026, 6, 22));

        months.Should().HaveCount(7);
        months[0].Month.Should().Be(new DateOnly(2026, 4, 1));
        months[^1].Month.Should().Be(new DateOnly(2026, 10, 1));
        months.Should().OnlyContain(x => x.Unit == GanttTimeUnit.Month);
    }

    [Fact]
    public void BuildMonths_UsesQuarterBucketsSoLongProjectFitsOverview()
    {
        var input = new ProjectGanttInputModel
        {
            StartDate = new DateOnly(2025, 2, 2),
            FinishDate = new DateOnly(2027, 5, 10)
        };

        var months = ProjectGanttService.BuildMonths(input, new DateOnly(2026, 7, 13));

        months.Should().HaveCount(10);
        months.Should().OnlyContain(x => x.Unit == GanttTimeUnit.Quarter);
    }

    [Fact]
    public void BuildMonths_DoesNotExtendProjectRangeToGlobalArchiveDate()
    {
        var input = new ProjectGanttInputModel
        {
            StartDate = new DateOnly(2026, 1, 1),
            FinishDate = new DateOnly(2026, 12, 31)
        };

        var months = ProjectGanttService.BuildMonths(input, new DateOnly(2030, 6, 1));

        months[0].Month.Should().Be(new DateOnly(2026, 1, 1));
        months[^1].EndMonth.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void BuildProgressLinePoints_UsesRedForBehindAndBlueForAhead()
    {
        var months = ProjectGanttService.BuildMonths(new ProjectGanttInputModel
        {
            StartDate = new DateOnly(2026, 1, 1),
            FinishDate = new DateOnly(2026, 1, 31)
        });
        var tasks = new List<ProjectGanttTaskInputModel>
        {
            new() { PlannedStartDate = new DateOnly(2026, 1, 1), PlannedFinishDate = new DateOnly(2026, 1, 31), ProgressPercent = 25m },
            new() { PlannedStartDate = new DateOnly(2026, 1, 1), PlannedFinishDate = new DateOnly(2026, 1, 31), ProgressPercent = 80m }
        };

        var points = ProjectGanttService.BuildProgressLinePoints(tasks, new DateOnly(2026, 1, 16), months);

        points[0].State.Should().Be(GanttProgressState.Behind);
        points[1].State.Should().Be(GanttProgressState.Ahead);
        points[0].PositionPercent.Should().BeLessThan(ProjectGanttService.GetTimelinePositionPercent(new DateOnly(2026, 1, 16), months));
        points[1].PositionPercent.Should().BeGreaterThan(ProjectGanttService.GetTimelinePositionPercent(new DateOnly(2026, 1, 16), months));
        points[0].VarianceDays.Should().BeNegative();
        points[1].VarianceDays.Should().BePositive();
        ProjectGanttService.GetProgressSummary(points[0]).Should().Contain("滯後");
        ProjectGanttService.GetProgressSummary(points[1]).Should().Contain("超前");
    }

    [Fact]
    public void BuildTaskWeights_MatchesReferenceTenItemDistribution()
    {
        var weights = ProjectGanttService.BuildTaskWeights(10);

        weights.Should().Equal(5m, 5m, 5m, 10m, 5m, 10m, 15m, 15m, 20m, 10m);
        weights.Sum().Should().Be(100m);
    }

    [Fact]
    public void CalculateActualProgress_UsesTaskWeights()
    {
        var tasks = new List<ProjectGanttTaskInputModel>
        {
            new() { ProgressPercent = 100m },
            new() { ProgressPercent = 50m }
        };

        var result = ProjectGanttService.CalculateActualProgress(tasks, [20m, 80m]);

        result.Should().Be(60m);
    }

    [Theory]
    [InlineData(100, GanttProgressState.Behind, GanttTaskVisualState.Completed)]
    [InlineData(45, GanttProgressState.Ahead, GanttTaskVisualState.Ahead)]
    [InlineData(45, GanttProgressState.Behind, GanttTaskVisualState.AtRisk)]
    public void GetTaskVisualState_UsesProgressAndScheduleState(
        decimal progress,
        GanttProgressState progressState,
        GanttTaskVisualState expected)
    {
        var task = new ProjectGanttTaskInputModel
        {
            PlannedStartDate = new DateOnly(2026, 7, 1),
            PlannedFinishDate = new DateOnly(2026, 7, 31),
            ProgressPercent = progress
        };

        var result = ProjectGanttService.GetTaskVisualState(
            task,
            new DateOnly(2026, 7, 14),
            progressState);

        result.Should().Be(expected);
    }

    [Fact]
    public void GetTaskVisualState_MarksFutureZeroProgressTaskAsNotStarted()
    {
        var task = new ProjectGanttTaskInputModel
        {
            PlannedStartDate = new DateOnly(2026, 8, 1),
            PlannedFinishDate = new DateOnly(2026, 8, 15),
            ProgressPercent = 0m
        };

        var result = ProjectGanttService.GetTaskVisualState(
            task,
            new DateOnly(2026, 7, 14),
            GanttProgressState.OnSchedule);

        result.Should().Be(GanttTaskVisualState.NotStarted);
    }

    [Theory]
    [InlineData("目前被外部审批阻塞", GanttTaskVisualState.Blocked)]
    [InlineData("等待客户确认设计稿", GanttTaskVisualState.Waiting)]
    public void GetTaskVisualState_UsesProgressDescriptionSignals(
        string description,
        GanttTaskVisualState expected)
    {
        var task = new ProjectGanttTaskInputModel
        {
            PlannedStartDate = new DateOnly(2026, 7, 1),
            PlannedFinishDate = new DateOnly(2026, 7, 31),
            ProgressPercent = 40m,
            ProgressDescription = description
        };

        var result = ProjectGanttService.GetTaskVisualState(
            task,
            new DateOnly(2026, 7, 14),
            GanttProgressState.OnSchedule);

        result.Should().Be(expected);
    }

    [Fact]
    public void GetTaskVisualState_MarksOverdueIncompleteTaskAsAtRisk()
    {
        var task = new ProjectGanttTaskInputModel
        {
            PlannedStartDate = new DateOnly(2026, 6, 1),
            PlannedFinishDate = new DateOnly(2026, 6, 30),
            ProgressPercent = 80m
        };

        var result = ProjectGanttService.GetTaskVisualState(
            task,
            new DateOnly(2026, 7, 14),
            GanttProgressState.OnSchedule);

        result.Should().Be(GanttTaskVisualState.AtRisk);
    }
}
