using FluentAssertions;
using ProjectManager.Web.Security;

namespace ProjectManager.Tests.Security;

public sealed class RoleSelectionTests
{
    [Theory]
    [InlineData(true, RoleNames.DataViewer)]
    [InlineData(true, RoleNames.Leader, RoleNames.SubCaseContact)]
    [InlineData(false, RoleNames.SubCaseContact)]
    [InlineData(false, RoleNames.DataViewer, RoleNames.ProjectStaff)]
    public void Normalize_requires_exactly_one_primary_role(
        bool expectedSuccess,
        params string[] selectedRoles)
    {
        var result = RoleSelection.Normalize(selectedRoles);

        result.Succeeded.Should().Be(expectedSuccess);
    }
}
