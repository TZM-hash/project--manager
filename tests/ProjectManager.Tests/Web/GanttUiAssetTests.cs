using FluentAssertions;

namespace ProjectManager.Tests.Web;

public sealed class GanttUiAssetTests
{
    [Fact]
    public void Web_gantt_uses_black_plan_frame_and_seven_visual_states()
    {
        var partial = ReadRepositoryFile(
            "src", "ProjectManager.Web", "Pages", "Shared", "_ProjectGanttPanel.cshtml");
        var css = FrontendAssetStructureTests.ReadCssLayers();

        partial.Should().Contain("gantt-visual-legend");
        partial.Should().Contain("gantt-bar-progress-label");
        partial.Should().Contain("GetTaskVisualState");
        partial.Should().Contain("GetProgressStageCssClass");
        partial.Should().Contain("gantt-task-milestone");
        partial.Should().Contain("gantt-legend-milestone");
        partial.Should().Contain("負責人");
        partial.Should().Contain("前置工作");
        partial.Should().Contain("實際開始");
        partial.Should().Contain("逾期");

        css.Should().Contain("border: 2px solid #111827");
        css.Should().Contain(".gantt-task-state-completed");
        css.Should().Contain(".gantt-task-state-in-progress");
        css.Should().Contain(".gantt-task-state-ahead");
        css.Should().Contain(".gantt-task-state-at-risk");
        css.Should().Contain(".gantt-task-state-waiting");
        css.Should().Contain(".gantt-task-state-blocked");
        css.Should().Contain(".gantt-task-state-not-started");
        partial.Should().Contain("class=\"gantt-chart-overlay\"");
        partial.Should().Contain("class=\"gantt-progress-line\"");
        partial.Should().Contain("viewBox=\"0 0 100 @taskRows.Count\"");
        partial.Should().Contain("previousX = point.PositionPercent");
        partial.Should().Contain("previousY = currentY");
        partial.Should().NotContain("gantt-progress-line-row");
        partial.Should().NotContain("class=\"gantt-chart-redline\"");

        css.Should().Contain(".gantt-panel .gantt-progress-line");
        css.Should().Contain(".gantt-chart-overlay");
        css.Should().Contain("width: 100%");
        css.Should().NotContain(".gantt-progress-line-row");
        css.Should().Contain("z-index: 6");
        css.Should().Contain("stroke-width: 2");
        css.Should().Contain("stroke-dasharray: none");
        css.Should().Contain(".gantt-progress-line .is-behind");
        css.Should().Contain(".gantt-progress-line .is-ahead");
    }

    [Fact]
    public void Gantt_print_view_reuses_web_visual_rules_and_keeps_overlay_inside_timeline()
    {
        var printView = ReadRepositoryFile(
            "src", "ProjectManager.Web", "Pages", "Workbench", "Projects", "GanttPrint.cshtml");
        var css = FrontendAssetStructureTests.ReadCssLayers();

        printView.Should().Contain("gantt-print-milestone");
        printView.Should().Contain("GetProgressStageCssClass");
        printView.Should().Contain("GetTaskVisualState");
        printView.Should().Contain("GetTaskVisualStateCssClass");
        printView.Should().Contain("gantt-print-task-state-badge");
        printView.Should().Contain("gantt-print-timeline-overlay");
        printView.Should().Contain("gantt-print-progress-marker");
        printView.Should().Contain("計劃：");
        printView.Should().NotContain("<small>實際：");

        css.Should().Contain("--gantt-print-row-height");
        css.Should().Contain(".gantt-print-timeline-overlay");
        css.Should().Contain("left: var(--gantt-print-timeline-left)");
        css.Should().Contain("width: var(--gantt-print-timeline-width)");
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProjectManager.sln")))
        {
            directory = directory.Parent;
        }

        var root = directory ?? throw new DirectoryNotFoundException("Cannot locate ProjectManager.sln.");
        return File.ReadAllText(Path.Combine(new[] { root.FullName }.Concat(pathParts).ToArray()))
            .ReplaceLineEndings("\n");
    }

}
