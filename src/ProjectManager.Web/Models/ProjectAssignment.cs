namespace ProjectManager.Web.Models;

public sealed class ProjectAssignment
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public string RoleInProject { get; set; } = string.Empty;
}
