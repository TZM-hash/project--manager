using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectManager.Web.Services;

public static partial class RichTextSanitizer
{
    public static readonly IReadOnlyList<RichTextColorOption> ColorOptions =
    [
        new("#111827", "深色"),
        new("#2563eb", "蓝色"),
        new("#dc2626", "红色"),
        new("#16a34a", "绿色"),
        new("#f59e0b", "橙色")
    ];

    private static readonly HashSet<string> AllowedColors = ColorOptions
        .Select(x => x.Value)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var input = value.Replace("\r\n", "\n").Replace('\r', '\n');
        var output = new StringBuilder(input.Length);
        var openSpans = 0;
        var lastIndex = 0;

        foreach (Match match in HtmlTagRegex().Matches(input))
        {
            AppendEncodedText(output, input[lastIndex..match.Index]);
            AppendAllowedTag(output, match.Value, ref openSpans);
            lastIndex = match.Index + match.Length;
        }

        AppendEncodedText(output, input[lastIndex..]);

        while (openSpans > 0)
        {
            output.Append("</span>");
            openSpans--;
        }

        var normalized = NormalizeBreaks(output.ToString()).Trim();
        return string.IsNullOrWhiteSpace(StripHtml(normalized)) ? null : normalized;
    }

    private static void AppendAllowedTag(StringBuilder output, string tag, ref int openSpans)
    {
        var lower = tag.ToLowerInvariant();
        if (lower.StartsWith("<br", StringComparison.Ordinal))
        {
            output.Append("<br>");
            return;
        }

        if (lower.StartsWith("<div", StringComparison.Ordinal) ||
            lower.StartsWith("</div", StringComparison.Ordinal) ||
            lower.StartsWith("<p", StringComparison.Ordinal) ||
            lower.StartsWith("</p", StringComparison.Ordinal))
        {
            output.Append("<br>");
            return;
        }

        if (lower.StartsWith("</span", StringComparison.Ordinal) ||
            lower.StartsWith("</font", StringComparison.Ordinal))
        {
            if (openSpans > 0)
            {
                output.Append("</span>");
                openSpans--;
            }

            return;
        }

        if (!lower.StartsWith("<span", StringComparison.Ordinal) &&
            !lower.StartsWith("<font", StringComparison.Ordinal))
        {
            return;
        }

        var color = ExtractColor(tag);
        if (color is null)
        {
            return;
        }

        output.Append("<span style=\"color:");
        output.Append(color);
        output.Append("\">");
        openSpans++;
    }

    private static string? ExtractColor(string tag)
    {
        var styleMatch = ColorStyleRegex().Match(tag);
        var color = styleMatch.Success ? styleMatch.Groups[1].Value : null;
        if (string.IsNullOrWhiteSpace(color))
        {
            var dataMatch = DataColorRegex().Match(tag);
            color = dataMatch.Success ? dataMatch.Groups[1].Value : null;
        }

        if (string.IsNullOrWhiteSpace(color))
        {
            var fontMatch = FontColorRegex().Match(tag);
            color = fontMatch.Success ? fontMatch.Groups[1].Value : null;
        }

        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        color = NormalizeColor(color);
        return AllowedColors.Contains(color) ? color : null;
    }

    private static string NormalizeColor(string value)
    {
        var color = value.Trim().ToLowerInvariant();
        if (color.StartsWith('#'))
        {
            return color;
        }

        var rgb = RgbColorRegex().Match(color);
        if (!rgb.Success)
        {
            return color;
        }

        var red = int.Parse(rgb.Groups[1].Value);
        var green = int.Parse(rgb.Groups[2].Value);
        var blue = int.Parse(rgb.Groups[3].Value);
        return $"#{red:x2}{green:x2}{blue:x2}";
    }

    private static void AppendEncodedText(StringBuilder output, string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        var decoded = WebUtility.HtmlDecode(text)
            .Replace('\u00a0', ' ');
        output.Append(WebUtility.HtmlEncode(decoded).Replace("\n", "<br>"));
    }

    private static string NormalizeBreaks(string value)
    {
        var compact = RepeatedBreakRegex().Replace(value, "<br><br>");
        compact = LeadingBreakRegex().Replace(compact, string.Empty);
        return TrailingBreakRegex().Replace(compact, string.Empty);
    }

    private static string StripHtml(string value)
    {
        return HtmlTagRegex().Replace(WebUtility.HtmlDecode(value), string.Empty);
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("color\\s*:\\s*(#[0-9a-fA-F]{6}|rgb\\(\\s*\\d{1,3}\\s*,\\s*\\d{1,3}\\s*,\\s*\\d{1,3}\\s*\\))", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ColorStyleRegex();

    [GeneratedRegex("data-color\\s*=\\s*[\"']?(#[0-9a-fA-F]{6}|rgb\\(\\s*\\d{1,3}\\s*,\\s*\\d{1,3}\\s*,\\s*\\d{1,3}\\s*\\))", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex DataColorRegex();

    [GeneratedRegex("\\scolor\\s*=\\s*[\"']?(#[0-9a-fA-F]{6}|rgb\\(\\s*\\d{1,3}\\s*,\\s*\\d{1,3}\\s*,\\s*\\d{1,3}\\s*\\))", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex FontColorRegex();

    [GeneratedRegex("^rgb\\(\\s*(\\d{1,3})\\s*,\\s*(\\d{1,3})\\s*,\\s*(\\d{1,3})\\s*\\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex RgbColorRegex();

    [GeneratedRegex("(<br>\\s*){3,}", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex RepeatedBreakRegex();

    [GeneratedRegex("^(<br>\\s*)+", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex LeadingBreakRegex();

    [GeneratedRegex("(\\s*<br>)+$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex TrailingBreakRegex();
}

public sealed record RichTextColorOption(string Value, string Label);
