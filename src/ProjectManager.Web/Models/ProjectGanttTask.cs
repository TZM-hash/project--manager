namespace ProjectManager.Web.Models;

public sealed class ProjectGanttTask
{
    public int Id { get; set; }

    public int ProjectGanttPlanId { get; set; }

    public ProjectGanttPlan? ProjectGanttPlan { get; set; }

    public int SortOrder { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedFinishDate { get; set; }

    public decimal ProgressPercent { get; set; }

    public string? ProgressDescription { get; set; }
}
