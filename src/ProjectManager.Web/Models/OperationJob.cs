namespace ProjectManager.Web.Models;

public enum OperationJobType
{
    FullExport = 1,
    FullImport = 2,
    ProjectBulkDelete = 3,
    MaintenanceBulkDelete = 4
}

public enum OperationJobStatus
{
    Queued = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
    Cancelled = 5
}

public sealed class OperationJob
{
    public int Id { get; set; }

    public OperationJobType Type { get; set; }

    public OperationJobStatus Status { get; set; } = OperationJobStatus.Queued;

    public string RequestedByUserId { get; set; } = string.Empty;

    public ApplicationUser? RequestedByUser { get; set; }

    public string? PayloadJson { get; set; }

    public int ProgressPercent { get; set; }

    public string? StatusMessage { get; set; }

    public string? ResultSummary { get; set; }

    public string? ErrorDetails { get; set; }

    public string? InputRelativePath { get; set; }

    public string? OutputRelativePath { get; set; }

    public string? OutputFileName { get; set; }

    public string? OutputContentType { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
