namespace ProjectManager.Web.Models;

public sealed class AuditLog
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int? ProjectId { get; set; }

    public string? ProjectNumber { get; set; }

    public string? ChangeSummary { get; set; }

    public string? ChangeDetailsJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
