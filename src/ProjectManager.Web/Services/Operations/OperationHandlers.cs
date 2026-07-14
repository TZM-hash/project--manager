using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services.Operations;

public interface IOperationJobHandler
{
    OperationJobType Type { get; }

    Task ExecuteAsync(OperationJob job, CancellationToken cancellationToken);
}

public sealed class FullExportOperationHandler(
    DataExchangeService dataExchangeService,
    OperationFileStore fileStore,
    OperationJobService jobs) : IOperationJobHandler
{
    public OperationJobType Type => OperationJobType.FullExport;

    public async Task ExecuteAsync(OperationJob job, CancellationToken cancellationToken)
    {
        await jobs.ReportProgressAsync(job.Id, 10, "正在整理匯出資料", cancellationToken);
        var export = await dataExchangeService.ExportAllAsync(cancellationToken);
        await using var stream = new MemoryStream(export.Contents, writable: false);
        var stored = await fileStore.SaveAsync("output", export.FileName, stream, cancellationToken);
        await jobs.CompleteAsync(
            job.Id,
            $"全量資料已匯出，檔案大小 {stored.Length:N0} 位元組。",
            stored.RelativePath,
            export.FileName,
            export.ContentType,
            cancellationToken);
    }
}

public sealed class FullImportOperationHandler(
    DataExchangeService dataExchangeService,
    OperationFileStore fileStore,
    OperationJobService jobs) : IOperationJobHandler
{
    public OperationJobType Type => OperationJobType.FullImport;

    public async Task ExecuteAsync(OperationJob job, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(job.InputRelativePath))
        {
            throw new InvalidOperationException("匯入工作缺少來源檔案。");
        }

        await jobs.ReportProgressAsync(job.Id, 10, "正在讀取匯入檔案", cancellationToken);
        await using var stream = fileStore.OpenRead(job.InputRelativePath);
        var result = await dataExchangeService.ImportAllAsync(stream, job.RequestedByUserId, cancellationToken);
        var summary = result.Summary;
        if (result.Errors.Count > 0)
        {
            summary += $"；前 {Math.Min(20, result.Errors.Count)} 筆錯誤：{string.Join("；", result.Errors.Take(20))}";
        }

        await jobs.CompleteAsync(job.Id, summary, null, null, null, cancellationToken);
    }
}

public sealed class ProjectBulkDeleteOperationHandler(
    ApplicationDbContext db,
    AuditLogService auditLogService,
    OperationJobService jobs) : IOperationJobHandler
{
    public OperationJobType Type => OperationJobType.ProjectBulkDelete;

    public async Task ExecuteAsync(OperationJob job, CancellationToken cancellationToken)
    {
        var payload = DeserializePayload(job.PayloadJson);
        var ids = payload.Ids.Distinct().ToArray();
        var projects = await db.Projects
            .Include(project => project.Assignments)
            .Include(project => project.PurchaseRequests)
            .Where(project => !project.IsDeleted && ids.Contains(project.Id))
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < projects.Count; index++)
        {
            var project = projects[index];
            var before = ProjectAuditChangeBuilder.CreateSnapshot(project);
            project.IsDeleted = true;
            project.UpdatedAt = now;
            project.UpdatedByUserId = job.RequestedByUserId;
            foreach (var request in project.PurchaseRequests)
            {
                request.IsDeleted = true;
                request.UpdatedAt = now;
            }

            await db.SaveChangesAsync(cancellationToken);
            await auditLogService.LogProjectChangeAsync(
                job.RequestedByUserId,
                "Delete",
                project.Id,
                project.ProjectNumber,
                $"背景批量刪除專案 {project.ProjectNumber}",
                ProjectAuditChangeBuilder.BuildDeleteChanges(before),
                cancellationToken);
            await jobs.ReportProgressAsync(
                job.Id,
                10 + (int)Math.Round((index + 1) / (double)Math.Max(1, projects.Count) * 80),
                $"已處理 {index + 1} / {projects.Count} 筆專案",
                cancellationToken);
        }

        await jobs.CompleteAsync(
            job.Id,
            $"嘗試 {ids.Length} 筆，成功 {projects.Count} 筆，失敗 {ids.Length - projects.Count} 筆。",
            null,
            null,
            null,
            cancellationToken);
    }

    private static BulkDeletePayload DeserializePayload(string? payload) =>
        JsonSerializer.Deserialize<BulkDeletePayload>(payload ?? string.Empty) ?? new BulkDeletePayload([]);
}

public sealed class MaintenanceBulkDeleteOperationHandler(
    MaintenanceOrderService maintenanceOrderService,
    OperationJobService jobs) : IOperationJobHandler
{
    public OperationJobType Type => OperationJobType.MaintenanceBulkDelete;

    public async Task ExecuteAsync(OperationJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<BulkDeletePayload>(job.PayloadJson ?? string.Empty) ?? new BulkDeletePayload([]);
        var ids = payload.Ids.Distinct().ToArray();
        await jobs.ReportProgressAsync(job.Id, 20, "正在刪除保養訂單", cancellationToken);
        var succeeded = await maintenanceOrderService.DeleteManyAsync(ids, cancellationToken);
        await jobs.CompleteAsync(
            job.Id,
            $"嘗試 {ids.Length} 筆，成功 {succeeded} 筆，失敗 {ids.Length - succeeded} 筆。",
            null,
            null,
            null,
            cancellationToken);
    }
}

public sealed class OperationHandlerDispatcher(IEnumerable<IOperationJobHandler> handlers)
{
    private readonly IReadOnlyDictionary<OperationJobType, IOperationJobHandler> handlersByType =
        handlers.ToDictionary(handler => handler.Type);

    public Task ExecuteAsync(OperationJob job, CancellationToken cancellationToken)
    {
        if (!handlersByType.TryGetValue(job.Type, out var handler))
        {
            throw new InvalidOperationException($"找不到工作類型 {job.Type} 的處理器。");
        }

        return handler.ExecuteAsync(job, cancellationToken);
    }
}

public sealed record BulkDeletePayload(int[] Ids);
