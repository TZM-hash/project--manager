using FluentAssertions;

namespace ProjectManager.Tests.Web;

public sealed class GanttUiAssetTests
{
    [Fact]
    public void Web_gantt_uses_black_plan_frame_and_seven_visual_states()
    {
        var partial = ReadRepositoryFile(
            "src", "ProjectManager.Web", "Pages", "Shared", "_ProjectGanttPanel.cshtml");
        var css = ReadRepositoryFile(
            "src", "ProjectManager.Web", "wwwroot", "css", "site.css");

        partial.Should().Contain("gantt-visual-legend");
        partial.Should().Contain("gantt-bar-progress-label");
        partial.Should().Contain("GetTaskVisualState");
        partial.Should().NotContain("gantt-task-milestone");
        partial.Should().NotContain("gantt-legend-milestone");

        css.Should().Contain("border: 2px solid #111827");
        css.Should().Contain(".gantt-task-state-completed");
        css.Should().Contain(".gantt-task-state-in-progress");
        css.Should().Contain(".gantt-task-state-ahead");
        css.Should().Contain(".gantt-task-state-at-risk");
        css.Should().Contain(".gantt-task-state-waiting");
        css.Should().Contain(".gantt-task-state-blocked");
        css.Should().Contain(".gantt-task-state-not-started");
        css.Should().Contain(".gantt-panel .gantt-progress-line-row");
        css.Should().Contain("z-index: 6");
        css.Should().Contain("stroke-width: 2");
        css.Should().Contain("stroke-dasharray: none");
        css.Should().Contain(".gantt-progress-line-row .is-behind");
        css.Should().Contain(".gantt-progress-line-row .is-ahead");
    }

    [Fact]
    public void Gantt_visual_redesign_does_not_change_print_view()
    {
        var printView = ReadRepositoryFile(
            "src", "ProjectManager.Web", "Pages", "Workbench", "Projects", "GanttPrint.cshtml");

        printView.Should().NotContain("gantt-visual-legend");
        printView.Should().NotContain("gantt-task-state-badge");
        printView.Should().NotContain("gantt-bar-progress-label");
        printView.Should().NotContain("gantt-task-milestone");
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
