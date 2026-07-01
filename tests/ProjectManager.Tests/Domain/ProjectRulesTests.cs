using FluentAssertions;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Domain;

public sealed class ProjectRulesTests
{
    [Fact]
    public void ValidateProject_rejects_negative_amounts_and_invalid_percentages()
    {
        var project = new Project
        {
            Year = 2026,
            ProjectNumber = "P-001",
            Name = "Test Project",
            ProjectAmount = -1,
            ProgressPercent = 101,
            CollectionPercent = -1
        };

        var errors = ProjectRules.ValidateProject(project, statusIsClosed: false);

        errors.Should().Contain("Project amount cannot be negative.");
        errors.Should().Contain("Progress percent must be between 0 and 100.");
        errors.Should().Contain("Collection percent must be between 0 and 100.");
    }

    [Fact]
    public void ValidateProject_requires_closed_year_month_when_status_is_closed()
    {
        var project = new Project
        {
            Year = 2026,
            ProjectNumber = "P-002",
            Name = "Closed Project",
            ProjectAmount = 1000,
            ProgressPercent = 100,
            CollectionPercent = 100
        };

        var errors = ProjectRules.ValidateProject(project, statusIsClosed: true);

        errors.Should().Contain("Closed year/month is required when project status is closed.");
    }

    [Fact]
    public void NormalizeClosedYearMonth_sets_day_to_first_day_of_month()
    {
        var normalized = ProjectRules.NormalizeClosedYearMonth(new DateOnly(2026, 7, 18));

        normalized.Should().Be(new DateOnly(2026, 7, 1));
    }
}
