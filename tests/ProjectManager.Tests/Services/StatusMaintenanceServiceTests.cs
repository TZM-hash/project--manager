using FluentAssertions;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class StatusMaintenanceServiceTests
{
    [Fact]
    public async Task DeleteAsync_rejects_status_currently_used_by_projects()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var status = new ProjectStatus
        {
            Code = "InUse",
            Name = "使用中",
            SortOrder = 10,
            Style = new ProjectStatusStyle()
        };
        db.Projects.Add(new Project
        {
            Year = 2026,
            ProjectNumber = "P-STATUS-1",
            Name = "Status Project",
            Status = status
        });
        await db.SaveChangesAsync();
        var service = new StatusMaintenanceService(db);

        var result = await service.DeleteAsync(status.Id, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("Status is currently used by projects and cannot be deleted.");
        db.ProjectStatuses.Should().Contain(x => x.Id == status.Id);
    }

    [Fact]
    public async Task DeleteAsync_removes_unused_status_and_style()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var status = new ProjectStatus
        {
            Code = "Unused",
            Name = "未使用",
            SortOrder = 10,
            Style = new ProjectStatusStyle()
        };
        db.ProjectStatuses.Add(status);
        await db.SaveChangesAsync();
        var service = new StatusMaintenanceService(db);

        var result = await service.DeleteAsync(status.Id, CancellationToken.None);

        result.Success.Should().BeTrue();
        db.ProjectStatuses.Should().NotContain(x => x.Id == status.Id);
        db.ProjectStatusStyles.Should().NotContain(x => x.StatusId == status.Id);
    }
}
