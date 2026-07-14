using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ProjectManager.Web.Data;

namespace ProjectManager.Web.Services.Operations;

public sealed class OperationalHealthService(
    ApplicationDbContext db,
    ExceptionLogStore exceptionLogs,
    OperationWorkerHeartbeat heartbeat,
    IOptions<OperationalMonitoringOptions> options,
    TimeProvider timeProvider)
{
    private readonly OperationalMonitoringOptions settings = options.Value;

    public async Task<OperationalHealthSnapshot> BuildAsync(CancellationToken cancellationToken)
    {
        var database = await CheckDatabaseAsync(cancellationToken);
        var backup = await CheckBackupAsync(cancellationToken);
        var disk = CheckDisk();
        var recentExceptions = await exceptionLogs.CountRecentAsync(TimeSpan.FromHours(24), cancellationToken);
        var exceptionStatus = recentExceptions switch
        {
            0 => new OperationalStatus("例外日誌", OperationalStatusLevel.Healthy, "最近 24 小時沒有未處理例外。"),
            <= 5 => new OperationalStatus("例外日誌", OperationalStatusLevel.Warning, $"最近 24 小時記錄 {recentExceptions} 筆例外。"),
            _ => new OperationalStatus("例外日誌", OperationalStatusLevel.Critical, $"最近 24 小時記錄 {recentExceptions} 筆例外，請儘速檢查。")
        };
        var worker = CheckWorker();
        return new OperationalHealthSnapshot(database, backup, disk, exceptionStatus, worker, timeProvider.GetUtcNow());
    }

    private async Task<OperationalStatus> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? new("資料庫", OperationalStatusLevel.Healthy, "資料庫連線正常。")
                : new("資料庫", OperationalStatusLevel.Critical, "資料庫無法連線。");
        }
        catch (Exception exception)
        {
            return new("資料庫", OperationalStatusLevel.Critical, $"資料庫連線失敗：{exception.Message}");
        }
    }

    private async Task<OperationalStatus> CheckBackupAsync(CancellationToken cancellationToken)
    {
        if (db.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) != true)
        {
            return new("資料庫備份", OperationalStatusLevel.Unknown, "目前資料庫提供者不支援 SQL Server 備份查詢。");
        }

        try
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose) await connection.OpenAsync(cancellationToken);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT MAX(backup_finish_date) FROM msdb.dbo.backupset WHERE database_name = DB_NAME() AND type = 'D'";
                var value = await command.ExecuteScalarAsync(cancellationToken);
                if (value is null or DBNull)
                {
                    return new("資料庫備份", OperationalStatusLevel.Warning, "查無最近一次完整備份記錄。");
                }

                var backupAt = new DateTimeOffset(Convert.ToDateTime(value));
                var age = timeProvider.GetUtcNow() - backupAt.ToUniversalTime();
                var level = age <= TimeSpan.FromHours(36)
                    ? OperationalStatusLevel.Healthy
                    : age <= TimeSpan.FromDays(7)
                        ? OperationalStatusLevel.Warning
                        : OperationalStatusLevel.Critical;
                return new("資料庫備份", level, $"最近完整備份：{backupAt.ToLocalTime():yyyy-MM-dd HH:mm}。");
            }
            finally
            {
                if (shouldClose) await connection.CloseAsync();
            }
        }
        catch (Exception exception)
        {
            return new("資料庫備份", OperationalStatusLevel.Unknown, $"無法讀取備份狀態：{exception.Message}");
        }
    }

    private OperationalStatus CheckDisk()
    {
        try
        {
            var dataRoot = Path.GetFullPath(settings.DataRootPath);
            Directory.CreateDirectory(dataRoot);
            var root = Path.GetPathRoot(dataRoot) ?? dataRoot;
            var drive = new DriveInfo(root);
            var freeGb = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
            var freePercent = drive.TotalSize == 0 ? 0 : drive.AvailableFreeSpace / (double)drive.TotalSize * 100;
            var level = freeGb < 5 || freePercent < 5
                ? OperationalStatusLevel.Critical
                : freeGb < 15 || freePercent < 15
                    ? OperationalStatusLevel.Warning
                    : OperationalStatusLevel.Healthy;
            return new("磁碟空間", level, $"可用 {freeGb:0.0} GB（{freePercent:0.0}%）。");
        }
        catch (Exception exception)
        {
            return new("磁碟空間", OperationalStatusLevel.Unknown, $"無法讀取磁碟狀態：{exception.Message}");
        }
    }

    private OperationalStatus CheckWorker()
    {
        if (heartbeat.LastHeartbeatAt is not { } lastHeartbeat)
        {
            return new("背景工作服務", OperationalStatusLevel.Unknown, "尚未收到背景工作服務心跳。");
        }

        var age = timeProvider.GetUtcNow() - lastHeartbeat;
        var level = age <= TimeSpan.FromMinutes(1)
            ? OperationalStatusLevel.Healthy
            : age <= TimeSpan.FromMinutes(5)
                ? OperationalStatusLevel.Warning
                : OperationalStatusLevel.Critical;
        return new("背景工作服務", level, $"最近心跳：{lastHeartbeat.ToLocalTime():yyyy-MM-dd HH:mm:ss}。");
    }
}

public enum OperationalStatusLevel
{
    Healthy,
    Warning,
    Critical,
    Unknown
}

public sealed record OperationalStatus(string Name, OperationalStatusLevel Level, string Detail);

public sealed record OperationalHealthSnapshot(
    OperationalStatus Database,
    OperationalStatus Backup,
    OperationalStatus Disk,
    OperationalStatus ExceptionLog,
    OperationalStatus Worker,
    DateTimeOffset CheckedAt);

public sealed class DatabaseReadinessHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database connection is available.")
                : HealthCheckResult.Unhealthy("Database connection is unavailable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database readiness check failed.", exception);
        }
    }
}
