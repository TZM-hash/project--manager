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

    public static bool CanViewAllBusinessData(this ClaimsPrincipal user)
    {
        return user.CanManageAllBusinessData() || user.IsInRole(RoleNames.DataViewer);
    }

    public static bool IsRegularUser(this ClaimsPrincipal user)
    {
        return user.IsInformationAdministrator() ||
               user.IsInRole(RoleNames.DataViewer) ||
               user.IsInRole(RoleNames.ProjectStaff) ||
               user.IsInRole(RoleNames.SubCaseContact);
    }
}
