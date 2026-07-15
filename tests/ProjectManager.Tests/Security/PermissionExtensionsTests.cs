using System.Security.Claims;
using FluentAssertions;
using ProjectManager.Web.Security;

namespace ProjectManager.Tests.Security;

public sealed class PermissionExtensionsTests
{
    [Theory]
    [InlineData(RoleNames.Administrator, true, true, true)]
    [InlineData(RoleNames.Leader, false, true, true)]
    [InlineData(RoleNames.ProjectStaff, false, false, true)]
    [InlineData(RoleNames.Viewer, false, false, true)]
    [InlineData(RoleNames.SubCaseContact, false, false, true)]
    public void Permission_levels_are_downward_compatible(
        string role,
        bool isSystemAdministrator,
        bool canManageAllBusinessData,
        bool isRegularUser)
    {
        var principal = CreatePrincipal(role);

        principal.IsSystemAdministrator().Should().Be(isSystemAdministrator);
        principal.CanManageAllBusinessData().Should().Be(canManageAllBusinessData);
        principal.IsRegularUser().Should().Be(isRegularUser);
    }

    [Fact]
    public void Role_display_names_expose_the_three_management_levels()
    {
        RoleNames.GetDisplayName(RoleNames.Administrator).Should().Be("系統管理員");
        RoleNames.GetDisplayName(RoleNames.Leader).Should().Be("資訊管理員");
        RoleNames.GetDisplayName(RoleNames.ProjectStaff).Should().Be("一般使用者");
        RoleNames.Assignable.Should().Equal(
            RoleNames.Administrator,
            RoleNames.Leader,
            RoleNames.ProjectStaff,
            RoleNames.SubCaseContact);
        RoleNames.LegacyRegularRoles.Should().Equal(
            RoleNames.Viewer,
            RoleNames.SubCaseContact);
    }

    [Theory]
    [InlineData(true, RoleNames.ProjectStaff)]
    [InlineData(true, RoleNames.Leader, RoleNames.SubCaseContact)]
    [InlineData(false, RoleNames.SubCaseContact)]
    [InlineData(false, RoleNames.Administrator, RoleNames.Leader)]
    public void Role_selection_requires_exactly_one_primary_level(
        bool expectedSuccess,
        params string[] selectedRoles)
    {
        var selectionType = typeof(RoleNames).Assembly.GetType("ProjectManager.Web.Security.RoleSelection");
        selectionType.Should().NotBeNull();
        var normalize = selectionType!.GetMethod("Normalize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        normalize.Should().NotBeNull();

        var result = normalize!.Invoke(null, [selectedRoles]);
        result.Should().NotBeNull();
        var succeeded = (bool)(result!.GetType().GetProperty("Succeeded")!.GetValue(result) ?? false);

        succeeded.Should().Be(expectedSuccess);
    }

    private static ClaimsPrincipal CreatePrincipal(string role)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim(ClaimTypes.Role, role)
            ],
            "Test"));
    }
}
