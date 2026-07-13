namespace ProjectManager.Web.Services;

public sealed record MetricInsight(
    string Label,
    string Value,
    string Hint = "",
    string Accent = "primary",
    Dictionary<string, string?>? RouteValues = null);

public sealed record ChartSlice(string Label, decimal Value, decimal Percent, string Color);

public static class ChartPalette
{
    private static readonly string[] Colors =
    [
        "#2563eb",
        "#14b8a6",
        "#f59e0b",
        "#8b5cf6",
        "#ef4444",
        "#64748b"
    ];

    public static IReadOnlyList<ChartSlice> BuildSlices(IEnumerable<(string Label, decimal Value)> source)
    {
        var rows = source
            .Where(x => x.Value > 0)
            .ToList();
        var total = rows.Sum(x => x.Value);

        if (total <= 0)
        {
            return [];
        }

        // 百分比在服務端算好，頁面只负责用 CSS/SVG 展示，避免 Razor 里堆业务計算。
        return rows
            .Select((x, index) => new ChartSlice(
                x.Label,
                x.Value,
                Math.Round(x.Value / total * 100, 1),
                Colors[index % Colors.Length]))
            .ToList();
    }

    public static IReadOnlyList<ChartSlice> BuildBars(IEnumerable<(string Label, decimal Value)> source)
    {
        var rows = source
            .Where(x => x.Value > 0)
            .ToList();
        var max = rows.Count == 0 ? 0 : rows.Max(x => x.Value);

        if (max <= 0)
        {
            return [];
        }

        return rows
            .Select((x, index) => new ChartSlice(
                x.Label,
                x.Value,
                Math.Round(x.Value / max * 100, 1),
                Colors[index % Colors.Length]))
            .ToList();
    }
}
