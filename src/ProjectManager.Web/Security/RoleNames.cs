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

    public static readonly string[] Assignable =
    [
        Administrator,
        Leader,
        ProjectStaff,
        SubCaseContact
    ];

    public static readonly string[] PrimaryRoles =
    [
        Administrator,
        Leader,
        ProjectStaff
    ];

    public static readonly string[] LegacyRegularRoles =
    [
        Viewer,
        SubCaseContact
    ];

    public const string BusinessManagerRoles = Administrator + "," + Leader;

    public const string BusinessDataRoles = Administrator + "," + Leader + "," + ProjectStaff + "," + Viewer + "," + SubCaseContact;

    public static string GetDisplayName(string roleName)
    {
        return roleName switch
        {
            Administrator => "系統管理員",
            ProjectStaff => "一般使用者",
            Leader => "資訊管理員",
            Viewer => "一般使用者（舊查詢角色）",
            SubCaseContact => "子案對接人",
            _ => roleName
        };
    }
}
