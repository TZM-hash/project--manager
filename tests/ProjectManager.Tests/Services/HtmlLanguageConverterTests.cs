using FluentAssertions;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class HtmlLanguageConverterTests
{
    [Fact]
    public void ToSimplified_converts_visible_text_and_accessibility_attributes_only()
    {
        var converter = new HtmlLanguageConverter(new OpenCcConverterService());
        const string html = """
            <main aria-label="系統設定">
                <h1>系統設定</h1>
                <input value="專案原文" placeholder="請輸入專案名稱">
                <textarea>專案原文</textarea>
                <script>const label = "系統設定";</script>
            </main>
            """;

        var result = converter.ToSimplified(html);

        result.Should().Contain("aria-label=\"系统设置\"");
        result.Should().Contain("<h1>系统设置</h1>");
        result.Should().Contain("value=\"專案原文\"");
        result.Should().Contain("placeholder=\"请输入项目名称\"");
        result.Should().Contain("<textarea>專案原文</textarea>");
        result.Should().Contain("const label = \"系統設定\";");
    }
}
