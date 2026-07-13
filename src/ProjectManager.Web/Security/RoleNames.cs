namespace ProjectManager.Web.Security;

public static class RoleNames
{
    public const string Administrator = "Administrator";
    public const string ProjectStaff = "ProjectStaff";
    public const string Leader = "Leader";
    public const string Viewer = "Viewer";
    public const string SubCaseContact = "SubCaseContact";

    public static readonly string[] All =
    [
        Administrator,
        ProjectStaff,
        Leader,
        Viewer,
        SubCaseContact
    ];

    public static string GetDisplayName(string roleName)
    {
        return roleName switch
        {
            Administrator => "系統管理員",
            ProjectStaff => "專案人員",
            Leader => "主管",
            Viewer => "查詢人员",
            SubCaseContact => "子案對接人",
            _ => roleName
        };
    }
}
