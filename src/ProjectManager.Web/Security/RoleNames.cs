namespace ProjectManager.Web.Security;

public static class RoleNames
{
    public const string Administrator = "Administrator";
    public const string ProjectStaff = "ProjectStaff";
    public const string Leader = "Leader";
    public const string DataViewer = "DataViewer";
    public const string LegacyViewer = "Viewer";
    public const string SubCaseContact = "SubCaseContact";

    public static readonly string[] All =
    [
        Administrator,
        ProjectStaff,
        Leader,
        DataViewer,
        SubCaseContact
    ];

    public static readonly string[] Assignable =
    [
        Administrator,
        Leader,
        DataViewer,
        ProjectStaff,
        SubCaseContact
    ];

    public static readonly string[] PrimaryRoles =
    [
        Administrator,
        Leader,
        DataViewer,
        ProjectStaff
    ];

    public const string BusinessManagerRoles = Administrator + "," + Leader;
    public const string FullBusinessReadRoles = Administrator + "," + Leader + "," + DataViewer;

    public const string BusinessDataRoles = Administrator + "," + Leader + "," + DataViewer + "," + ProjectStaff + "," + SubCaseContact;

    public static string GetDisplayName(string roleName)
    {
        return roleName switch
        {
            Administrator => "系統管理員",
            ProjectStaff => "一般使用者",
            Leader => "資訊管理員",
            DataViewer => "資料查看員",
            SubCaseContact => "子案對接人",
            _ => roleName
        };
    }
}
