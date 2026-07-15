using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using ProjectManager.Web.Pages.Admin.Projects;
using PlanningCreateInput = ProjectManager.Web.Pages.Workbench.PlanningProjects.CreateModel.InputModel;
using PlanningEditInput = ProjectManager.Web.Pages.Workbench.PlanningProjects.EditModel.InputModel;

namespace ProjectManager.Tests.Web;

public sealed class InputLengthValidationTests
{
    [Fact]
    public void Project_fields_reject_values_longer_than_database_limits()
    {
        var input = new ProjectInputModel
        {
            Year = 2026,
            ParentCaseNumber = new string('A', 65),
            ProjectNumber = new string('P', 65),
            Name = "Valid project",
            StatusId = 1
        };

        var results = Validate(input);

        results.Should().Contain(result => result.MemberNames.Contains(nameof(ProjectInputModel.ParentCaseNumber)));
        results.Should().Contain(result => result.MemberNames.Contains(nameof(ProjectInputModel.ProjectNumber)));
    }

    [Fact]
    public void Purchase_request_number_rejects_values_longer_than_database_limit()
    {
        var input = new PurchaseInputModel
        {
            RequestNumber = new string('R', 65)
        };

        Validate(input).Should().ContainSingle(result =>
            result.MemberNames.Contains(nameof(PurchaseInputModel.RequestNumber)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Planning_project_fields_reject_values_longer_than_database_limits(bool isCreate)
    {
        object input = isCreate
            ? new PlanningCreateInput { Name = new string('N', 201), Vendor = new string('V', 201) }
            : new PlanningEditInput { Name = new string('N', 201), Vendor = new string('V', 201) };

        var results = Validate(input);

        results.Should().Contain(result => result.MemberNames.Contains("Name"));
        results.Should().Contain(result => result.MemberNames.Contains("Vendor"));
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
