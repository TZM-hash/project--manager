using System.Text.RegularExpressions;
using FluentAssertions;

namespace ProjectManager.Tests.Web;

public sealed class UiEffectsAssetTests
{
    [Fact]
    public void Apple_motion_style_has_low_medium_and_high_effect_profiles()
    {
        var css = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "css", "site.css");
        var js = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "js", "site.js");

        css.Should().Contain("body.motion-apple.ui-effects-low");
        css.Should().Contain("body.motion-apple.ui-effects-medium");
        css.Should().Contain("body.motion-apple.ui-effects-high");
        js.Should().Contain("initMotionStylePreview");
        js.Should().Contain("initApplePressFeedback");
        js.Should().Contain("motion-apple");
    }

    [Fact]
    public void Clear_glass_theme_includes_translucent_surfaces_and_live_preview()
    {
        var css = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "css", "site.css");
        var js = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "js", "site.js");

        css.Should().Contain("body.theme-clear-glass");
        css.Should().Contain("backdrop-filter: blur(24px) saturate(170%)");
        css.Should().Contain(".theme-preview-glass");
        js.Should().Contain("initThemePreview");
        js.Should().Contain("theme-clear-glass");
    }

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
        css.Should().Contain("height: 3px");
        css.Should().Contain("transform: scaleX(0.72)");
        css.Should().Contain(".ui-click-ripple");
        js.Should().Contain("initUiPageTransitions");
        js.Should().Contain("initClickRipples");
        js.Should().Contain("data-ui-page-transition");
        js.Should().Contain("const navigationDelay = appleMotion ? 45 : 30");
        js.Should().NotContain("}, 170);");
    }

    [Fact]
    public void Global_font_options_use_local_cross_platform_fallback_stacks()
    {
        var css = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "css", "site.css");
        var layout = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_Layout.cshtml");

        css.Should().Contain("body.font-system-default");
        css.Should().Contain("body.font-microsoft-yahei");
        css.Should().Contain("body.font-microsoft-jhenghei");
        css.Should().Contain("body.font-chinese-serif");
        css.Should().Contain("body.font-chinese-kai");
        css.Should().Contain("\"PingFang SC\"");
        css.Should().Contain("\"Noto Sans CJK SC\"");
        layout.Should().Contain("ToFontCssClass");
        layout.Should().NotContain("fonts.googleapis.com");
    }

    [Fact]
    public void Global_font_setting_uses_single_dropdown_with_live_preview()
    {
        var settings = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Admin", "Settings", "Index.cshtml");
        var css = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "css", "site.css");
        var js = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "js", "site.js");

        settings.Should().Contain("data-global-font-select");
        settings.Should().Contain("data-global-font-preview");
        settings.Should().Contain("value=\"ChineseKai\"");
        settings.Should().NotContain("font-option-card");
        css.Should().Contain(".font-select-panel");
        css.Should().Contain(".font-preview-kai");
        js.Should().Contain("initGlobalFontPreview");
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
