using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using ProjectManager.Web.Pages.Admin.Projects;

namespace ProjectManager.Tests.Web;

public sealed class CreateFlowRegressionTests
{
    private static readonly string[] CreateViews =
    [
        Path.Combine("Pages", "Admin", "Projects", "_ProjectForm.cshtml"),
        Path.Combine("Pages", "Workbench", "PlanningProjects", "Create.cshtml"),
        Path.Combine("Pages", "Admin", "Users", "Create.cshtml"),
        Path.Combine("Pages", "Admin", "Vendors", "Create.cshtml"),
        Path.Combine("Pages", "Admin", "Statuses", "Edit.cshtml"),
        Path.Combine("Pages", "Admin", "MaintenanceOrders", "Create.cshtml"),
        Path.Combine("Pages", "Settlements", "Create.cshtml")
    ];

    private static readonly string[] CreateHandlers =
    [
        Path.Combine("Pages", "Admin", "Projects", "Create.cshtml.cs"),
        Path.Combine("Pages", "Workbench", "PlanningProjects", "Create.cshtml.cs"),
        Path.Combine("Pages", "Admin", "Users", "Create.cshtml.cs"),
        Path.Combine("Pages", "Admin", "Vendors", "Create.cshtml.cs"),
        Path.Combine("Pages", "Admin", "Statuses", "Edit.cshtml.cs"),
        Path.Combine("Pages", "Admin", "MaintenanceOrders", "Create.cshtml.cs"),
        Path.Combine("Pages", "Settlements", "Create.cshtml.cs")
    ];

    [Fact]
    public void Manual_create_forms_show_all_errors_and_processing_feedback()
    {
        foreach (var relativePath in CreateViews)
        {
            var source = ReadWebFile(relativePath);
            source.Should().Contain("asp-validation-summary=\"All\"", relativePath);
            source.Should().Contain("data-processing-label", relativePath);
        }
    }

    [Fact]
    public void Successful_create_handlers_set_a_flash_message()
    {
        foreach (var relativePath in CreateHandlers)
        {
            ReadWebFile(relativePath).Should().Contain("TempData[\"SuccessMessage\"]", relativePath);
        }
    }

    [Fact]
    public void Layout_and_feedback_script_render_and_focus_save_results()
    {
        var layout = ReadWebFile(Path.Combine("Pages", "Shared", "_Layout.cshtml"));
        var partial = ReadWebFile(Path.Combine("Pages", "Shared", "_FlashMessages.cshtml"));
        var feedback = ReadWebFile(Path.Combine("wwwroot", "js", "core", "feedback.js"));

        layout.Should().Contain("partial name=\"_FlashMessages\"");
        partial.Should().Contain("SuccessMessage");
        partial.Should().Contain("ErrorMessage");
        feedback.Should().Contain("validation-summary-errors");
        feedback.Should().Contain("scrollIntoView");
        feedback.Should().Contain("input-validation-error");
    }

    [Fact]
    public void Project_input_validation_identifies_required_and_out_of_range_fields()
    {
        var input = new ProjectInputModel
        {
            Year = 1999,
            ProjectNumber = string.Empty,
            Name = string.Empty,
            ProgressPercent = 101,
            ProjectAmount = -1,
            CollectionPercent = -1
        };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true)
            .Should().BeFalse();

        var members = results.SelectMany(x => x.MemberNames).ToHashSet();
        members.Should().Contain(nameof(ProjectInputModel.Year));
        members.Should().Contain(nameof(ProjectInputModel.ProjectNumber));
        members.Should().Contain(nameof(ProjectInputModel.Name));
        members.Should().Contain(nameof(ProjectInputModel.ProgressPercent));
        members.Should().Contain(nameof(ProjectInputModel.ProjectAmount));
        members.Should().Contain(nameof(ProjectInputModel.CollectionPercent));
    }

    [Fact]
    public void User_and_planning_edit_flows_use_role_validation_and_success_feedback()
    {
        var userCreate = ReadWebFile(Path.Combine("Pages", "Admin", "Users", "Create.cshtml.cs"));
        var userEdit = ReadWebFile(Path.Combine("Pages", "Admin", "Users", "Edit.cshtml.cs"));
        var userEditView = ReadWebFile(Path.Combine("Pages", "Admin", "Users", "Edit.cshtml"));
        var planningCreateView = ReadWebFile(Path.Combine("Pages", "Workbench", "PlanningProjects", "Create.cshtml"));
        var planningEdit = ReadWebFile(Path.Combine("Pages", "Workbench", "PlanningProjects", "Edit.cshtml.cs"));
        var planningEditView = ReadWebFile(Path.Combine("Pages", "Workbench", "PlanningProjects", "Edit.cshtml"));

        userCreate.Should().Contain("RoleSelection.Normalize");
        userCreate.Should().Contain("roleResult.Succeeded");
        userEdit.Should().Contain("RoleSelection.Normalize");
        userEdit.Should().Contain("TempData[\"SuccessMessage\"]");
        userEdit.Should().Contain("IsolationLevel.Serializable");
        planningEdit.Should().Contain("TempData[\"SuccessMessage\"]");
        planningEdit.Should().Contain("ResolveActiveProjectStaffUserIdAsync");
        userEditView.Should().Contain("asp-validation-summary=\"All\"");
        userEditView.Should().Contain("data-processing-label");
        planningEditView.Should().Contain("asp-validation-summary=\"All\"");
        planningEditView.Should().Contain("data-processing-label");
        planningCreateView.Should().NotContain(" multiple");
        planningEditView.Should().NotContain(" multiple");
    }

    private static string ReadWebFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProjectManager.sln")))
        {
            directory = directory.Parent;
        }

        var root = directory ?? throw new DirectoryNotFoundException("Cannot locate ProjectManager.sln.");
        return File.ReadAllText(Path.Combine(root.FullName, "src", "ProjectManager.Web", relativePath))
            .ReplaceLineEndings("\n");
    }
}
