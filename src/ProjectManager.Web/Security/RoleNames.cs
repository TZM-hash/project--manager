namespace ProjectManager.Web.Security;

public static class RoleNames
{
    public const string Administrator = "Administrator";
    public const string ProjectStaff = "ProjectStaff";
    public const string Leader = "Leader";
    public const string Viewer = "Viewer";

    public static readonly string[] All =
    [
        Administrator,
        ProjectStaff,
        Leader,
        Viewer
    ];
}
