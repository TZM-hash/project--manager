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

    public static string GetDisplayName(string roleName)
    {
        return roleName switch
        {
            Administrator => "系统管理员",
            ProjectStaff => "项目人员",
            Leader => "领导",
            Viewer => "查询人员",
            _ => roleName
        };
    }
}
