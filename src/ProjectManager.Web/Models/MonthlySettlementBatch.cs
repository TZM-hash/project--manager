namespace ProjectManager.Web.Models;

public sealed class MonthlySettlementBatch
{
    public int Id { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public int BatchNumber { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser? CreatedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? Notes { get; set; }

    public ICollection<MonthlySettlementItem> Items { get; } = new List<MonthlySettlementItem>();
}
