using ProjectManager.Web.Models;

namespace ProjectManager.Web.Extensions;

public static class EnumExtensions
{
    public static string GetDisplayName(this MaintenanceMethod method)
    {
        return method switch
        {
            MaintenanceMethod.OnSite => "现场保养",
            MaintenanceMethod.Remote => "远程保养",
            MaintenanceMethod.Both => "均有",
            _ => method.ToString()
        };
    }
}
