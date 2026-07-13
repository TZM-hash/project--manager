using ProjectManager.Web.Models;

namespace ProjectManager.Web.Extensions;

public static class EnumExtensions
{
    public static string GetDisplayName(this MaintenanceMethod method)
    {
        return method switch
        {
            MaintenanceMethod.OnSite => "现场保養",
            MaintenanceMethod.Remote => "远程保養",
            MaintenanceMethod.Both => "均有",
            _ => method.ToString()
        };
    }

    public static string GetDisplayName(this ProjectType projectType)
    {
        return projectType switch
        {
            ProjectType.Maintenance => "保養",
            ProjectType.Engineering => "工程",
            _ => projectType.ToString()
        };
    }
}
