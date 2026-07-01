using FluentAssertions;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;
using System.Text.Json;

namespace ProjectManager.Tests.Services;

public sealed class AuditLogServiceTests
{
    [Fact]
    public async Task LogAsync_writes_audit_entry()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        db.Users.Add(new ApplicationUser { Id = "user-1", UserName = "user" });
        await db.SaveChangesAsync();
        var service = new AuditLogService(db);

        await service.LogAsync(
            "user-1",
            "Update",
            "Project",
            "P-001",
            "Updated project progress.",
            CancellationToken.None);

        db.AuditLogs.Should().ContainSingle(log =>
            log.UserId == "user-1" &&
            log.Action == "Update" &&
            log.EntityName == "Project" &&
            log.EntityId == "P-001" &&
            log.Description == "Updated project progress.");
    }

    [Fact]
    public async Task LogProjectChangeAsync_writes_project_metadata_and_structured_details()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        db.Users.Add(new ApplicationUser { Id = "user-1", UserName = "user" });
        await db.SaveChangesAsync();
        var service = new AuditLogService(db);

        var details = new[]
        {
            new AuditChangeDetail("Field", "进度", "30%", "45%", "项目进度")
        };

        await service.LogProjectChangeAsync(
            "user-1",
            "Update",
            projectId: 42,
            projectNumber: "P-042",
            changeSummary: "更新项目 P-042：进度 30% -> 45%",
            details,
            CancellationToken.None);

        var log = db.AuditLogs.Single();
        log.UserId.Should().Be("user-1");
        log.Action.Should().Be("Update");
        log.EntityName.Should().Be("Project");
        log.EntityId.Should().Be("42");
        log.ProjectId.Should().Be(42);
        log.ProjectNumber.Should().Be("P-042");
        log.ChangeSummary.Should().Be("更新项目 P-042：进度 30% -> 45%");
        log.Description.Should().Be("更新项目 P-042：进度 30% -> 45%");

        using var document = JsonDocument.Parse(log.ChangeDetailsJson!);
        var first = document.RootElement.EnumerateArray().Single();
        first.GetProperty("category").GetString().Should().Be("Field");
        first.GetProperty("label").GetString().Should().Be("进度");
        first.GetProperty("before").GetString().Should().Be("30%");
        first.GetProperty("after").GetString().Should().Be("45%");
        first.GetProperty("scope").GetString().Should().Be("项目进度");
    }
}
