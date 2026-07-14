using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services.Operations;

namespace ProjectManager.Tests.Services;

public sealed class OperationJobServiceTests
{
    [Fact]
    public async Task Queue_claim_progress_and_complete_persist_job_lifecycle()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var _ = connection;
        await using var __ = db;
        db.Users.Add(new ApplicationUser { Id = "admin-1", UserName = "admin", DisplayName = "管理員" });
        await db.SaveChangesAsync();
        var heartbeat = new OperationWorkerHeartbeat();
        var service = new OperationJobService(db, heartbeat);

        var queued = await service.QueueAsync(
            OperationJobType.FullExport,
            "admin-1",
            payload: null,
            inputRelativePath: null,
            CancellationToken.None);
        queued.Status.Should().Be(OperationJobStatus.Queued);
        var claimed = await service.ClaimNextAsync(CancellationToken.None);
        await service.ReportProgressAsync(claimed!.Id, 45, "正在整理專案資料", CancellationToken.None);
        await service.CompleteAsync(
            claimed.Id,
            "已匯出 12 筆專案",
            "output/export.xlsx",
            "export.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            CancellationToken.None);

        db.ChangeTracker.Clear();
        var saved = db.OperationJobs.Single();
        saved.Status.Should().Be(OperationJobStatus.Succeeded);
        saved.ProgressPercent.Should().Be(100);
        saved.StartedAt.Should().NotBeNull();
        saved.CompletedAt.Should().NotBeNull();
        saved.ResultSummary.Should().Contain("12");
        heartbeat.LastHeartbeatAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Job_queries_enforce_owner_unless_caller_can_view_all()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var _ = connection;
        await using var __ = db;
        db.Users.AddRange(
            new ApplicationUser { Id = "user-1", UserName = "user1", DisplayName = "一號" },
            new ApplicationUser { Id = "user-2", UserName = "user2", DisplayName = "二號" });
        await db.SaveChangesAsync();
        var service = new OperationJobService(db, new OperationWorkerHeartbeat());
        var job = await service.QueueAsync(OperationJobType.ProjectBulkDelete, "user-1", "{\"ids\":[1]}", null, CancellationToken.None);

        (await service.GetAsync(job.Id, "user-1", false, CancellationToken.None)).Should().NotBeNull();
        (await service.GetAsync(job.Id, "user-2", false, CancellationToken.None)).Should().BeNull();
        (await service.GetAsync(job.Id, "user-2", true, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task File_store_rejects_path_escape_and_roundtrips_utf8_content()
    {
        var root = Path.Combine(Path.GetTempPath(), $"operation-store-{Guid.NewGuid():N}");
        try
        {
            var store = new OperationFileStore(Options.Create(new OperationStorageOptions { RootPath = root }));
            await using var input = new MemoryStream(Encoding.UTF8.GetBytes("繁體測試"));

            var saved = await store.SaveAsync("input", "測試.xlsx", input, CancellationToken.None);
            await using var opened = store.OpenRead(saved.RelativePath);
            using var reader = new StreamReader(opened, Encoding.UTF8);

            (await reader.ReadToEndAsync()).Should().Be("繁體測試");
            saved.RelativePath.Should().StartWith("input/");
            var escape = () => store.OpenRead("../outside.txt");
            escape.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
