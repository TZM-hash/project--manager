using System.Security.Claims;

namespace ProjectManager.Web.Security;

public static class PermissionExtensions
{
    public static bool IsSystemAdministrator(this ClaimsPrincipal user)
    {
        return user.IsInRole(RoleNames.Administrator);
    }

    public static bool IsInformationAdministrator(this ClaimsPrincipal user)
    {
        return user.IsSystemAdministrator() || user.IsInRole(RoleNames.Leader);
    }

    public static bool CanManageAllBusinessData(this ClaimsPrincipal user)
    {
        return user.IsInformationAdministrator();
    }

    public static bool IsRegularUser(this ClaimsPrincipal user)
    {
        return user.IsInformationAdministrator() ||
               user.IsInRole(RoleNames.ProjectStaff) ||
               user.IsInRole(RoleNames.Viewer) ||
               user.IsInRole(RoleNames.SubCaseContact);
    }
}
