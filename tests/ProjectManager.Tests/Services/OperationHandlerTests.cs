using System.Text.Json;
using FluentAssertions;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;
using ProjectManager.Web.Services.Operations;

namespace ProjectManager.Tests.Services;

public sealed class OperationHandlerTests
{
    [Fact]
    public async Task Project_bulk_delete_handler_reports_progress_soft_deletes_and_completes_job()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var _ = connection;
        await using var __ = db;
        var user = new ApplicationUser { Id = "admin-1", UserName = "admin", DisplayName = "管理員" };
        var project = new Project
        {
            Year = 2026,
            ProjectNumber = "DELETE-001",
            Name = "待刪除專案",
            Status = new ProjectStatus { Code = "doing-delete", Name = "執行中", SortOrder = 1 },
            UpdatedByUser = user
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        var jobs = new OperationJobService(db, new OperationWorkerHeartbeat());
        var queued = await jobs.QueueAsync(
            OperationJobType.ProjectBulkDelete,
            user.Id,
            JsonSerializer.Serialize(new BulkDeletePayload([project.Id])),
            null,
            CancellationToken.None);
        var claimed = await jobs.ClaimNextAsync(CancellationToken.None);
        var handler = new ProjectBulkDeleteOperationHandler(db, new AuditLogService(db), jobs);

        await handler.ExecuteAsync(claimed!, CancellationToken.None);

        db.ChangeTracker.Clear();
        db.Projects.Single().IsDeleted.Should().BeTrue();
        var savedJob = db.OperationJobs.Single(job => job.Id == queued.Id);
        savedJob.Status.Should().Be(OperationJobStatus.Succeeded);
        savedJob.ResultSummary.Should().Contain("成功 1 筆");
        db.AuditLogs.Should().ContainSingle(log => log.Action == "Delete");
    }
}
