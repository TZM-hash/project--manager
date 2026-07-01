using FluentAssertions;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

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
}
