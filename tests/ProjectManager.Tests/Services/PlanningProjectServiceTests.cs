using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class PlanningProjectServiceTests
{
    [Fact]
    public async Task GetPlanningProjectsPageAsync_normalizes_paging_and_returns_totals()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        for (var i = 1; i <= 25; i++)
        {
            db.PlanningProjects.Add(new PlanningProject
            {
                Name = $"Planning {i:00}",
                UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();
        var service = new PlanningProjectService(db);

        var page = await service.GetPlanningProjectsPageAsync(
            pageNumber: -3,
            pageSize: 25,
            CancellationToken.None);

        page.PageNumber.Should().Be(1);
        page.PageSize.Should().Be(20);
        page.TotalCount.Should().Be(25);
        page.TotalPages.Should().Be(2);
        page.Items.Should().HaveCount(20);
    }

    [Fact]
    public async Task DeleteManyAsync_soft_deletes_existing_projects_only()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var first = new PlanningProject { Name = "First" };
        var second = new PlanningProject { Name = "Second" };
        db.PlanningProjects.AddRange(first, second);
        await db.SaveChangesAsync();
        var service = new PlanningProjectService(db);

        var deletedCount = await service.DeleteManyAsync([first.Id, second.Id, 999], CancellationToken.None);

        deletedCount.Should().Be(2);
        var remaining = await db.PlanningProjects.CountAsync(x => !x.IsDeleted);
        remaining.Should().Be(0);
    }
}
