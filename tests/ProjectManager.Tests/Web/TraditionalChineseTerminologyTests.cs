using FluentAssertions;

namespace ProjectManager.Tests.Web;

public sealed class TraditionalChineseTerminologyTests
{
    private static readonly string[] ForbiddenTerms =
    [
        "当前",
        "确定",
        "推进",
        "颜色",
        "系统",
        "用户",
        "数据",
        "项目",
        "导入",
        "导出",
        "打印"
    ];

    [Fact]
    public void User_facing_source_uses_traditional_chinese_as_the_canonical_text()
    {
        var root = RepositoryRoot();
        var files = Directory.EnumerateFiles(
                Path.Combine(root, "src", "ProjectManager.Web"),
                "*.*",
                SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase));

        var violations = files
            .SelectMany(path => ForbiddenTerms
                .Where(term => File.ReadAllText(path).Contains(term, StringComparison.Ordinal))
                .Select(term => $"{Path.GetRelativePath(root, path)}: {term}"))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        violations.Should().BeEmpty();
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
