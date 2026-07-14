using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services.Operations;

public sealed class OperationJobService(
    ApplicationDbContext db,
    OperationWorkerHeartbeat heartbeat)
{
    public async Task<OperationJob> QueueAsync(
        OperationJobType type,
        string requestedByUserId,
        string? payload,
        string? inputRelativePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestedByUserId))
        {
            throw new ArgumentException("找不到工作建立者。", nameof(requestedByUserId));
        }

        var now = DateTimeOffset.UtcNow;
        var job = new OperationJob
        {
            Type = type,
            Status = OperationJobStatus.Queued,
            RequestedByUserId = requestedByUserId,
            PayloadJson = payload,
            InputRelativePath = inputRelativePath,
            ProgressPercent = 0,
            StatusMessage = "已排入處理佇列",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.OperationJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return job;
    }

    public Task<OperationJob?> GetAsync(
        int id,
        string userId,
        bool canViewAll,
        CancellationToken cancellationToken) =>
        db.OperationJobs
            .AsNoTracking()
            .Include(job => job.RequestedByUser)
            .SingleOrDefaultAsync(job =>
                job.Id == id && (canViewAll || job.RequestedByUserId == userId),
                cancellationToken);

    public async Task<IReadOnlyList<OperationJob>> GetRecentAsync(
        string userId,
        bool canViewAll,
        int take,
        CancellationToken cancellationToken)
    {
        var query = db.OperationJobs
            .AsNoTracking()
            .Include(job => job.RequestedByUser)
            .Where(job => canViewAll || job.RequestedByUserId == userId);
        return await query
            .OrderByDescending(job => job.Id)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);
    }

    public async Task<OperationJob?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        heartbeat.Beat();
        var job = await db.OperationJobs
            .Where(item => item.Status == OperationJobStatus.Queued)
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        job.Status = OperationJobStatus.Running;
        job.StartedAt = now;
        job.UpdatedAt = now;
        job.ProgressPercent = Math.Max(1, job.ProgressPercent);
        job.StatusMessage = "背景工作已開始";
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return job;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(job).State = EntityState.Detached;
            return null;
        }
    }

    public async Task ReportProgressAsync(
        int id,
        int progressPercent,
        string statusMessage,
        CancellationToken cancellationToken)
    {
        heartbeat.Beat();
        var job = await RequiredRunningJobAsync(id, cancellationToken);
        job.ProgressPercent = Math.Clamp(progressPercent, 1, 99);
        job.StatusMessage = Truncate(statusMessage, 500);
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(
        int id,
        string resultSummary,
        string? outputRelativePath,
        string? outputFileName,
        string? outputContentType,
        CancellationToken cancellationToken)
    {
        heartbeat.Beat();
        var job = await RequiredRunningJobAsync(id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        job.Status = OperationJobStatus.Succeeded;
        job.ProgressPercent = 100;
        job.StatusMessage = "處理完成";
        job.ResultSummary = Truncate(resultSummary, 2000);
        job.OutputRelativePath = outputRelativePath;
        job.OutputFileName = Truncate(outputFileName, 260);
        job.OutputContentType = Truncate(outputContentType, 160);
        job.CompletedAt = now;
        job.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(
        int id,
        string errorDetails,
        CancellationToken cancellationToken)
    {
        heartbeat.Beat();
        var job = await db.OperationJobs.SingleAsync(item => item.Id == id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        job.Status = OperationJobStatus.Failed;
        job.StatusMessage = "處理失敗";
        job.ErrorDetails = Truncate(errorDetails, 8000);
        job.CompletedAt = now;
        job.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> RecoverAbandonedAsync(
        TimeSpan staleAfter,
        CancellationToken cancellationToken)
    {
        var staleBefore = DateTimeOffset.UtcNow.Subtract(staleAfter);
        var jobs = await db.OperationJobs
            .Where(job => job.Status == OperationJobStatus.Running)
            .ToListAsync(cancellationToken);
        var abandoned = jobs.Where(job => job.UpdatedAt < staleBefore).ToList();
        foreach (var job in abandoned)
        {
            job.Status = OperationJobStatus.Queued;
            job.StatusMessage = "服務中斷後重新排入佇列";
            job.StartedAt = null;
            job.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (abandoned.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return abandoned.Count;
    }

    private Task<OperationJob> RequiredRunningJobAsync(int id, CancellationToken cancellationToken) =>
        db.OperationJobs.SingleAsync(
            item => item.Id == id && item.Status == OperationJobStatus.Running,
            cancellationToken);

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}

public sealed class OperationWorkerHeartbeat
{
    private readonly object sync = new();
    private DateTimeOffset? lastHeartbeatAt;

    public DateTimeOffset? LastHeartbeatAt
    {
        get
        {
            lock (sync)
            {
                return lastHeartbeatAt;
            }
        }
    }

    public void Beat()
    {
        lock (sync)
        {
            lastHeartbeatAt = DateTimeOffset.UtcNow;
        }
    }
}
