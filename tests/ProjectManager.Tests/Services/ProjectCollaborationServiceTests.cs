using FluentAssertions;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class ProjectCollaborationServiceTests
{
    [Fact]
    public async Task Assigned_user_can_add_and_read_normalized_collaboration_record()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var _ = connection;
        await using var __ = db;
        var project = await SeedAsync(db);
        var service = new ProjectCollaborationService(db);

        var result = await service.AddAsync(
            new CollaborationCommand(project.Id, null, "staff-1", false, false, "風險", "  等待客戶\r\n確認  ", null),
            CancellationToken.None);
        var page = await service.GetPageAsync(project.Id, "staff-1", false, 1, 20, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Record!.Content.Should().Be("等待客戶\n確認");
        page.Items.Should().ContainSingle(record => record.Category == "風險");
    }

    [Fact]
    public async Task Unassigned_user_cannot_add_or_read_project_collaboration()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var _ = connection;
        await using var __ = db;
        var project = await SeedAsync(db);
        var service = new ProjectCollaborationService(db);

        var result = await service.AddAsync(
            new CollaborationCommand(project.Id, null, "other-1", false, false, "進度協作", "不應寫入", null),
            CancellationToken.None);
        var page = await service.GetPageAsync(project.Id, "other-1", false, 1, 20, CancellationToken.None);

        result.Success.Should().BeFalse();
        page.Items.Should().BeEmpty();
        page.CanAccess.Should().BeFalse();
    }

    [Fact]
    public async Task Update_rejects_stale_row_version_and_preserves_current_content()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var _ = connection;
        await using var __ = db;
        var project = await SeedAsync(db);
        var service = new ProjectCollaborationService(db);
        var added = await service.AddAsync(
            new CollaborationCommand(project.Id, null, "staff-1", false, false, "進度協作", "目前內容", null),
            CancellationToken.None);

        var result = await service.UpdateAsync(
            new CollaborationCommand(
                project.Id,
                added.Record!.Id,
                "staff-1",
                false,
                false,
                "進度協作",
                "過期內容",
                Convert.ToBase64String(new byte[8])),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.IsConcurrencyConflict.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Contain("已被其他使用者更新");
        db.ChangeTracker.Clear();
        db.ProjectCollaborationRecords.Single().Content.Should().Be("目前內容");
    }

    private static async Task<Project> SeedAsync(ProjectManager.Web.Data.ApplicationDbContext db)
    {
        var staff = new ApplicationUser { Id = "staff-1", UserName = "staff", DisplayName = "專案人員" };
        var other = new ApplicationUser { Id = "other-1", UserName = "other", DisplayName = "其他人" };
        var project = new Project
        {
            Year = 2026,
            ProjectNumber = "COLLAB-001",
            Name = "協作測試",
            Status = new ProjectStatus { Code = "doing-collab", Name = "執行中", SortOrder = 1 },
            UpdatedByUser = staff,
            Assignments = { new ProjectAssignment { User = staff, RoleInProject = "專案人員" } }
        };
        db.Users.Add(other);
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }
}
