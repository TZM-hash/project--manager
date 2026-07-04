using System.Text.RegularExpressions;
using FluentAssertions;

namespace ProjectManager.Tests.Web;

public sealed class UiEffectsAssetTests
{
    [Fact]
    public void Low_and_medium_levels_are_shifted_down_from_the_previous_profiles()
    {
        var css = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "css", "site.css");
        var js = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "js", "site.js");

        css.Should().Contain("body.ui-effects-low .app-bg-fx .app-bg-orb-3");
        css.Should().Contain("body.ui-effects-medium .tilt-card");
        js.Should().Contain("level === \"medium\" || level === \"high\"");
    }

    [Fact]
    public void High_level_includes_page_transition_and_click_feedback_effects()
    {
        var css = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "css", "site.css");
        var js = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "js", "site.js");

        css.Should().Contain("body.ui-effects-high .page-transition-overlay");
        css.Should().Contain(".ui-click-ripple");
        js.Should().Contain("initUiPageTransitions");
        js.Should().Contain("initClickRipples");
        js.Should().Contain("data-ui-page-transition");
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProjectManager.sln")))
        {
            directory = directory.Parent;
        }

        var root = directory ?? throw new DirectoryNotFoundException("Cannot locate ProjectManager.sln.");
        return File.ReadAllText(Path.Combine(new[] { root.FullName }.Concat(pathParts).ToArray()));
    }
}
