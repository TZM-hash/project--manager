using FluentAssertions;

namespace ProjectManager.Tests.Web;

public sealed class ProjectUiRegressionTests
{
    [Fact]
    public void Legacy_project_type_migration_backfills_zero_values_to_engineering()
    {
        var migrationsDirectory = RepositoryPath("src", "ProjectManager.Web", "Migrations");
        var migration = Directory
            .GetFiles(migrationsDirectory, "*RepairLegacyProjectTypes.cs")
            .Should().ContainSingle().Subject;
        var source = File.ReadAllText(migration);

        source.Should().Contain("UPDATE [Projects] SET [ProjectType] = 2 WHERE [ProjectType] = 0");
    }

    [Fact]
    public void Desktop_shell_keeps_wide_tables_inside_the_content_viewport()
    {
        var css = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "css", "site.css");

        css.Should().Contain("min-width: 0;");
        css.Should().Contain("max-width: calc(100vw - var(--sidebar-width));");
        css.Should().Contain("max-width: calc(100vw - var(--sidebar-collapsed-width));");
        css.Should().Contain(".data-table-wrap");
        css.Should().Contain("width: 100%;");
    }

    [Fact]
    public void Navigation_script_marks_the_current_module_and_opens_its_group()
    {
        var js = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "js", "site.js");

        js.Should().Contain("initActiveNavigation");
        js.Should().Contain("aria-current");
        js.Should().Contain("nav-group-open");
    }

    [Fact]
    public void Project_overview_keeps_analysis_optional_and_prioritizes_work_controls()
    {
        var page = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Index.cshtml");

        page.Should().Contain("<details class=\"project-analysis-panel data-work-surface\"");
        page.Should().Contain("<summary class=\"project-analysis-summary\"");
        page.IndexOf("data-filter-drawer", StringComparison.Ordinal)
            .Should().BeLessThan(page.IndexOf("project-analysis-panel", StringComparison.Ordinal));
    }

    [Fact]
    public void Project_filters_and_destructive_controls_have_specific_accessible_names()
    {
        var page = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Index.cshtml");

        page.Should().Contain("aria-label=\"搜尋專案名稱\"");
        page.Should().Contain("aria-label=\"篩選經辦人員\"");
        page.Should().Contain("aria-label=\"選擇全部專案\"");
        page.Should().Contain("aria-label=\"選擇專案 @project.ProjectNumber\"");
        page.Should().Contain("aria-label=\"刪除專案 @project.ProjectNumber\"");
    }

    [Fact]
    public void Clear_glass_theme_uses_opaque_surfaces_for_dense_work_areas()
    {
        var css = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "css", "site.css");

        css.Should().Contain("body.theme-clear-glass .data-work-surface");
        css.Should().Contain("background: rgba(255, 255, 255, 0.86);");
        css.Should().Contain("font-variant-numeric: tabular-nums;");
    }

    [Fact]
    public void Project_table_preserves_readable_widths_for_identity_and_people_columns()
    {
        var css = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "css", "site.css");

        css.Should().Contain(".data-table [data-column=\"projectNumber\"]");
        css.Should().Contain(".data-table [data-column=\"assignments\"]");
        css.Should().Contain("min-width: 8.5rem;");
        css.Should().Contain("min-width: 10rem;");
    }

    [Fact]
    public void Project_details_load_only_the_selected_server_tab()
    {
        var adminPage = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Details.cshtml");
        var workbenchPage = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "Projects", "Details.cshtml");
        var adminModel = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Details.cshtml.cs");

        adminPage.Should().Contain("data-detail-tabs-server");
        workbenchPage.Should().Contain("data-detail-tabs-server");
        adminPage.Should().Contain("asp-route-Tab=\"gantt\"");
        adminPage.Should().Contain("@if (Model.ActiveTab == \"audit\")");
        adminModel.Should().Contain("if (ActiveTab == \"gantt\")");
        adminModel.Should().Contain("if (ActiveTab == \"audit\")");
        adminModel.Should().Contain("AsSplitQuery()");
    }

    [Fact]
    public void Audit_trail_supports_database_paging_and_user_selected_page_sizes()
    {
        var partial = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_AuditTrail.cshtml");
        var service = ReadRepositoryFile("src", "ProjectManager.Web", "Services", "AuditTrailQueryService.cs");

        partial.Should().Contain("name=\"AuditPageSize\"");
        partial.Should().Contain("操作記錄分頁");
        service.Should().Contain("AllowedPageSizes = [10, 25, 50, 100]");
        service.Should().Contain(".Skip((currentPage - 1) * selectedPageSize)");
        service.Should().Contain(".Take(selectedPageSize)");
    }

    [Fact]
    public void Gantt_exposes_progress_tooltips_and_sticky_desktop_headers()
    {
        var partial = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_ProjectGanttPanel.cshtml");
        var print = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Workbench", "Projects", "GanttPrint.cshtml");
        var css = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "css", "site.css");

        partial.Should().Contain("gantt-progress-marker");
        partial.Should().Contain("GetProgressSummary");
        print.Should().Contain("GetProgressSummary(progressPoint)");
        css.Should().Contain(".gantt-panel .gantt-chart-header");
        css.Should().Contain("position: sticky;");
        css.Should().Contain("content: attr(data-tooltip)");
    }

    [Fact]
    public void Maintenance_orders_match_template_fields_and_support_grouped_column_visibility()
    {
        var index = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "MaintenanceOrders", "Index.cshtml");
        var create = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "MaintenanceOrders", "Create.cshtml");
        var css = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "css", "site.css");
        var script = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "js", "site.js");

        index.Should().Contain("maintenance-order-table");
        index.Should().Contain("data-column-group=\"remoteScope softwareScope hardwareScope\"");
        index.Should().Contain("data-column=\"description\"");
        index.Should().Contain("data-column-manager-table");
        create.Should().Contain("Input.ContractNumber");
        create.Should().Contain("Input.OnSiteSoftwareFrequency");
        create.Should().Contain("Input.MaintenanceDescription");
        css.Should().Contain(".maintenance-order-table");
        script.Should().Contain("data-column-group");
        script.Should().Contain("data-visible-column-count");
    }

    [Fact]
    public void Dynamic_project_values_can_opt_out_of_language_conversion()
    {
        var adminPage = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Projects", "Details.cshtml");
        var converter = ReadRepositoryFile("src", "ProjectManager.Web", "Services", "HtmlLanguageConverter.cs");

        adminPage.Should().Contain("data-language-preserve");
        converter.Should().Contain("data-language-preserve");
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        return File.ReadAllText(RepositoryPath(pathParts));
    }

    private static string RepositoryPath(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProjectManager.sln")))
        {
            directory = directory.Parent;
        }

        var root = directory ?? throw new DirectoryNotFoundException("Cannot locate ProjectManager.sln.");
        return Path.Combine(new[] { root.FullName }.Concat(pathParts).ToArray());
    }
}
