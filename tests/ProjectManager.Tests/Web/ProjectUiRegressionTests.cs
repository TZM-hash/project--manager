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
