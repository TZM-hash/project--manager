using FluentAssertions;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class ProjectVisualStateResolverTests
{
    [Theory]
    [InlineData("Closed", true, "complete")]
    [InlineData("Blocked", false, "blocked")]
    [InlineData("WaitingCollection", false, "waiting")]
    [InlineData("PendingClosure", false, "waiting")]
    [InlineData("Coding", false, "active")]
    [InlineData("TrialRun", false, "active")]
    [InlineData("CustomState", false, "neutral")]
    public void Status_semantics_are_derived_from_business_state(
        string code,
        bool isClosed,
        string expected)
    {
        ProjectVisualStateResolver.ResolveStatus(code, isClosed).CssKey.Should().Be(expected);
    }

    [Fact]
    public void Low_progress_alone_is_not_a_risk()
    {
        var now = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
        var project = CreateProject(progress: 10, collection: 10, updatedAt: now.AddDays(-2));

        ProjectVisualStateResolver.ResolveRisk(project, now).CssKey.Should().Be("normal");
    }

    [Fact]
    public void Collection_lag_is_a_warning()
    {
        var now = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
        var project = CreateProject(progress: 70, collection: 30, updatedAt: now.AddDays(-2));

        var state = ProjectVisualStateResolver.ResolveRisk(project, now);

        state.CssKey.Should().Be("warning");
        state.Label.Should().Be("收款落後");
    }

    [Fact]
    public void Stale_active_project_requires_attention()
    {
        var now = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
        var project = CreateProject(progress: 45, collection: 40, updatedAt: now.AddDays(-31));

        ProjectVisualStateResolver.ResolveRisk(project, now).CssKey.Should().Be("attention");
    }

    [Fact]
    public void Closed_project_is_complete_even_when_collection_is_lower()
    {
        var now = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
        var project = CreateProject(progress: 100, collection: 50, updatedAt: now.AddDays(-40), isClosed: true);

        ProjectVisualStateResolver.ResolveRisk(project, now).CssKey.Should().Be("complete");
    }

    private static Project CreateProject(
        decimal progress,
        decimal collection,
        DateTimeOffset updatedAt,
        bool isClosed = false)
    {
        return new Project
        {
            ProgressPercent = progress,
            CollectionPercent = collection,
            UpdatedAt = updatedAt,
            Status = new ProjectStatus
            {
                Code = isClosed ? "Closed" : "Coding",
                Name = isClosed ? "已結案" : "程式中",
                IsClosed = isClosed
            }
        };
    }
}
