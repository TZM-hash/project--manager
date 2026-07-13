using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectManager.Web.Services;

public sealed class HtmlLanguageConverter(OpenCcConverterService converter)
{
    private static readonly Regex TranslatableAttributePattern = new(
        @"(?<prefix>\s(?:placeholder|title|aria-label|alt)\s*=\s*(?<quote>[""']))(?<value>.*?)(?:\k<quote>)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> SkippedContentTags = new(
        ["script", "style", "code", "pre", "textarea"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly (string Source, string Target)[] SimplifiedInterfaceTerms =
    [
        ("系统设定", "系统设置"),
        ("状态设定", "状态设置"),
        ("设定", "设置"),
        ("专案", "项目"),
        ("介面", "界面"),
        ("使用者", "用户"),
        ("资料", "数据"),
        ("储存", "保存"),
        ("选单", "菜单"),
        ("登入", "登录"),
        ("汇入", "导入"),
        ("汇出", "导出"),
        ("列印", "打印"),
        ("程式", "程序"),
        ("目前", "当前"),
        ("检视", "查看"),
        ("建立", "创建")
    ];

    public string ToSimplified(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return html;
        }

        var output = new StringBuilder(html.Length);
        var index = 0;
        string? skippedTag = null;

        while (index < html.Length)
        {
            if (skippedTag is not null)
            {
                var closingIndex = html.IndexOf($"</{skippedTag}", index, StringComparison.OrdinalIgnoreCase);
                if (closingIndex < 0)
                {
                    output.Append(html, index, html.Length - index);
                    break;
                }

                output.Append(html, index, closingIndex - index);
                index = closingIndex;
                skippedTag = null;
                continue;
            }

            if (html[index] == '<')
            {
                var tagEnd = FindTagEnd(html, index);
                if (tagEnd < 0)
                {
                    output.Append(ConvertInterfaceText(html[index..]));
                    break;
                }

                var tag = html[index..(tagEnd + 1)];
                output.Append(ConvertAttributes(tag));

                var (tagName, isClosing, isSelfClosing) = ParseTag(tag);
                if (!isClosing && !isSelfClosing && tagName is not null && SkippedContentTags.Contains(tagName))
                {
                    skippedTag = tagName;
                }

                index = tagEnd + 1;
                continue;
            }

            var nextTag = html.IndexOf('<', index);
            var textEnd = nextTag < 0 ? html.Length : nextTag;
            output.Append(ConvertInterfaceText(html[index..textEnd]));
            index = textEnd;
        }

        return output.ToString();
    }

    private string ConvertAttributes(string tag)
    {
        if (tag.StartsWith("<!--", StringComparison.Ordinal)
            || tag.StartsWith("<!", StringComparison.Ordinal)
            || tag.StartsWith("<?", StringComparison.Ordinal))
        {
            return tag;
        }

        return TranslatableAttributePattern.Replace(tag, match =>
            match.Groups["prefix"].Value
            + ConvertInterfaceText(match.Groups["value"].Value)
            + match.Groups["quote"].Value);
    }

    private string ConvertInterfaceText(string text)
    {
        var decoded = WebUtility.HtmlDecode(text);
        var converted = converter.ToSimplified(decoded);
        foreach (var (source, target) in SimplifiedInterfaceTerms)
        {
            converted = converted.Replace(source, target, StringComparison.Ordinal);
        }

        return WebUtility.HtmlEncode(converted);
    }

    private static int FindTagEnd(string html, int startIndex)
    {
        if (html.AsSpan(startIndex).StartsWith("<!--", StringComparison.Ordinal))
        {
            var commentEnd = html.IndexOf("-->", startIndex + 4, StringComparison.Ordinal);
            return commentEnd < 0 ? -1 : commentEnd + 2;
        }

        var quote = '\0';
        for (var index = startIndex + 1; index < html.Length; index++)
        {
            var current = html[index];
            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
                continue;
            }

            if (current == '>')
            {
                return index;
            }
        }

        return -1;
    }

    private static (string? TagName, bool IsClosing, bool IsSelfClosing) ParseTag(string tag)
    {
        var content = tag.AsSpan(1, tag.Length - 2).Trim();
        if (content.IsEmpty || content[0] is '!' or '?')
        {
            return (null, false, false);
        }

        var isClosing = content[0] == '/';
        if (isClosing)
        {
            content = content[1..].TrimStart();
        }

        var length = 0;
        while (length < content.Length
               && (char.IsLetterOrDigit(content[length]) || content[length] is '-' or ':'))
        {
            length++;
        }

        var tagName = length == 0 ? null : content[..length].ToString();
        return (tagName, isClosing, content.EndsWith("/", StringComparison.Ordinal));
    }
}
