using System.Security.Claims;
using FluentAssertions;
using ProjectManager.Web.Security;

namespace ProjectManager.Tests.Security;

public sealed class PermissionExtensionsTests
{
    [Theory]
    [InlineData(RoleNames.Administrator, true, true, true, true)]
    [InlineData(RoleNames.Leader, false, true, true, true)]
    [InlineData(RoleNames.DataViewer, false, false, true, true)]
    [InlineData(RoleNames.ProjectStaff, false, false, false, true)]
    [InlineData(RoleNames.SubCaseContact, false, false, false, true)]
    public void Permission_levels_are_downward_compatible(
        string role,
        bool isSystemAdministrator,
        bool canManageAllBusinessData,
        bool canViewAllBusinessData,
        bool isRegularUser)
    {
        var principal = CreatePrincipal(role);

        principal.IsSystemAdministrator().Should().Be(isSystemAdministrator);
        principal.CanManageAllBusinessData().Should().Be(canManageAllBusinessData);
        principal.CanViewAllBusinessData().Should().Be(canViewAllBusinessData);
        principal.IsRegularUser().Should().Be(isRegularUser);
    }

    [Fact]
    public void Role_display_names_expose_the_four_primary_levels()
    {
        RoleNames.GetDisplayName(RoleNames.Administrator).Should().Be("系統管理員");
        RoleNames.GetDisplayName(RoleNames.Leader).Should().Be("資訊管理員");
        RoleNames.GetDisplayName(RoleNames.DataViewer).Should().Be("資料查看員");
        RoleNames.GetDisplayName(RoleNames.ProjectStaff).Should().Be("一般使用者");
        RoleNames.Assignable.Should().Equal(
            RoleNames.Administrator,
            RoleNames.Leader,
            RoleNames.DataViewer,
            RoleNames.ProjectStaff,
            RoleNames.SubCaseContact);
        RoleNames.PrimaryRoles.Should().Equal(
            RoleNames.Administrator,
            RoleNames.Leader,
            RoleNames.DataViewer,
            RoleNames.ProjectStaff);
        RoleNames.All.Should().NotContain(RoleNames.LegacyViewer);
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
