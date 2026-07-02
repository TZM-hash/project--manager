using FluentAssertions;
using ProjectManager.Web.Data;
using ProjectManager.Web.Security;

namespace ProjectManager.Tests.Data;

public sealed class SeedDataTests
{
    [Fact]
    public void Initial_statuses_include_closed_status_with_red_bold_style()
    {
        var closed = SeedData.InitialStatuses.Single(x => x.Code == "Closed");
        var style = SeedData.InitialStatusStyles.Single(x => x.StatusCode == "Closed");

        closed.Name.Should().Be("已结案");
        closed.IsClosed.Should().BeTrue();
        style.TextColor.Should().Be("#dc2626");
        style.IsBold.Should().BeTrue();
    }

    [Fact]
    public void Role_names_include_required_initial_roles()
    {
        RoleNames.All.Should().BeEquivalentTo(
            RoleNames.Administrator,
            RoleNames.ProjectStaff,
            RoleNames.Leader,
            RoleNames.Viewer,
            RoleNames.SubCaseContact);
    }
}
