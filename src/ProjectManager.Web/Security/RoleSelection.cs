namespace ProjectManager.Web.Security;

public static class RoleSelection
{
    public static RoleSelectionResult Normalize(IEnumerable<string>? selectedRoles)
    {
        var roles = (selectedRoles ?? [])
            .Where(RoleNames.Assignable.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var primaryRoleCount = roles.Count(RoleNames.PrimaryRoles.Contains);

        return primaryRoleCount == 1
            ? new RoleSelectionResult(true, roles, null)
            : new RoleSelectionResult(
                false,
                [],
                "請選擇一個主權限層級：系統管理員、資訊管理員或一般使用者。");
    }
}

public sealed record RoleSelectionResult(
    bool Succeeded,
    IReadOnlyList<string> Roles,
    string? ErrorMessage);
