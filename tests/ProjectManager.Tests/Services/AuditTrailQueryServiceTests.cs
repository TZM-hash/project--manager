using FluentAssertions;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class AuditTrailQueryServiceTests
{
    [Fact]
    public async Task BuildAsync_filters_and_paginates_project_logs_with_selected_page_size()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var now = DateTimeOffset.UtcNow;
        db.AuditLogs.AddRange(Enumerable.Range(1, 35).Select(index => new AuditLog
        {
            ProjectId = 7,
            EntityName = "Project",
            EntityId = "7",
            Action = index % 2 == 0 ? "Update" : "ProgressUpdate",
            Description = $"記錄 {index}",
            ChangeSummary = index == 30 ? "特殊關鍵字" : $"摘要 {index}",
            CreatedAt = now.AddMinutes(index)
        }));
        db.AuditLogs.Add(new AuditLog
        {
            ProjectId = 8,
            EntityName = "Project",
            EntityId = "8",
            Action = "Update",
            Description = "其他專案",
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        var service = new AuditTrailQueryService(db);
        var page = await service.BuildAsync(7, null, null, null, null, 2, 10, CancellationToken.None);
        var filtered = await service.BuildAsync(7, "特殊關鍵字", null, null, null, 1, 25, CancellationToken.None);

        page.TotalCount.Should().Be(35);
        page.PageNumber.Should().Be(2);
        page.PageSize.Should().Be(10);
        page.Logs.Should().HaveCount(10);
        page.ActionOptions.Select(x => x.Value).Should().BeEquivalentTo("Update", "ProgressUpdate");
        filtered.TotalCount.Should().Be(1);
        filtered.Logs.Should().ContainSingle(x => x.Summary == "特殊關鍵字");
    }

    [Fact]
    public async Task BuildAsync_falls_back_to_default_page_size_for_unknown_value()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var service = new AuditTrailQueryService(db);

        var result = await service.BuildAsync(1, null, null, null, null, 1, 999, CancellationToken.None);

        result.PageSize.Should().Be(AuditTrailQueryService.DefaultPageSize);
    }
}
