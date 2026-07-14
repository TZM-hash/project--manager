using FluentAssertions;

namespace ProjectManager.Tests.Web;

public sealed class FrontendAssetStructureTests
{
    [Fact]
    public void Layout_loads_four_css_layers_in_dependency_order()
    {
        var layout = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_Layout.cshtml");
        var baseIndex = layout.IndexOf("~/css/base.css", StringComparison.Ordinal);
        var componentsIndex = layout.IndexOf("~/css/components.css", StringComparison.Ordinal);
        var pagesIndex = layout.IndexOf("~/css/pages.css", StringComparison.Ordinal);
        var themesIndex = layout.IndexOf("~/css/themes.css", StringComparison.Ordinal);

        baseIndex.Should().BeGreaterThan(-1);
        baseIndex.Should().BeLessThan(componentsIndex);
        componentsIndex.Should().BeLessThan(pagesIndex);
        pagesIndex.Should().BeLessThan(themesIndex);
        layout.Should().NotContain("href=\"~/css/site.css\"");
    }

    [Fact]
    public void Site_script_loads_page_components_on_demand()
    {
        var script = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "js", "site.js");

        script.Should().Contain("import { initShell }");
        script.Should().Contain("document.querySelector(\"[data-column-manager], [data-row-spacing]\")");
        script.Should().Contain("import(\"./components/data-table.js\")");
        script.Should().Contain("document.querySelector(\"[data-gantt-editor]\")");
        script.Should().Contain("import(\"./components/gantt-editor.js\")");
        script.Should().Contain("document.querySelector(\"[data-theme-option], [data-motion-option], [data-global-font-picker]\")");
        script.Should().Contain("import(\"./pages/settings.js\")");
    }

    public static string ReadCssLayers()
    {
        return string.Join(
            Environment.NewLine,
            new[] { "base.css", "components.css", "pages.css", "themes.css" }
                .Select(file => ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "css", file)));
    }

    public static string ReadJavaScriptModules()
    {
        var root = Path.Combine(RepositoryRoot(), "src", "ProjectManager.Web", "wwwroot", "js");
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(root, "*.js", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(pathParts).ToArray()));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProjectManager.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Cannot locate ProjectManager.sln.");
    }
}
