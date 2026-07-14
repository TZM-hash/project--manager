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

    public bool IsMilestone { get; set; }

    public string? OwnerUserId { get; set; }

    public ApplicationUser? OwnerUser { get; set; }

    public int? PredecessorTaskId { get; set; }

    public ProjectGanttTask? PredecessorTask { get; set; }

    public ICollection<ProjectGanttTask> DependentTasks { get; } = new List<ProjectGanttTask>();

    public DateOnly? ActualStartDate { get; set; }

    public DateOnly? ActualFinishDate { get; set; }
}
