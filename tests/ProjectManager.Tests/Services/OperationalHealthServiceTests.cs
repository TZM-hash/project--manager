using FluentAssertions;
using Microsoft.Extensions.Options;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Services.Operations;

namespace ProjectManager.Tests.Services;

public sealed class OperationalHealthServiceTests
{
    [Fact]
    public async Task Exception_log_store_writes_utf8_jsonl_and_counts_recent_errors()
    {
        var root = Path.Combine(Path.GetTempPath(), $"exception-log-{Guid.NewGuid():N}");
        try
        {
            var store = new ExceptionLogStore(Options.Create(new OperationalMonitoringOptions
            {
                LogRootPath = root,
                DataRootPath = root
            }));

            await store.WriteAsync(
                new InvalidOperationException("繁體錯誤訊息"),
                "/test",
                "user-1",
                "trace-1",
                CancellationToken.None);

            (await store.CountRecentAsync(TimeSpan.FromHours(24), CancellationToken.None)).Should().Be(1);
            var file = Directory.GetFiles(root, "exceptions-*.jsonl").Single();
            (await File.ReadAllTextAsync(file)).Should().Contain("繁體錯誤訊息");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Operational_health_reports_database_backup_fallback_disk_logs_and_worker_heartbeat()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var _ = connection;
        await using var __ = db;
        var root = Path.Combine(Path.GetTempPath(), $"health-{Guid.NewGuid():N}");
        try
        {
            var options = Options.Create(new OperationalMonitoringOptions
            {
                LogRootPath = Path.Combine(root, "logs"),
                DataRootPath = root
            });
            var logs = new ExceptionLogStore(options);
            await logs.WriteAsync(new Exception("測試例外"), "/health", null, "trace", CancellationToken.None);
            var heartbeat = new OperationWorkerHeartbeat();
            heartbeat.Beat();
            var service = new OperationalHealthService(db, logs, heartbeat, options, TimeProvider.System);

            var snapshot = await service.BuildAsync(CancellationToken.None);

            snapshot.Database.Level.Should().Be(OperationalStatusLevel.Healthy);
            snapshot.Backup.Level.Should().Be(OperationalStatusLevel.Unknown);
            snapshot.Disk.Level.Should().NotBe(OperationalStatusLevel.Unknown);
            snapshot.ExceptionLog.Detail.Should().Contain("1");
            snapshot.Worker.Level.Should().Be(OperationalStatusLevel.Healthy);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
