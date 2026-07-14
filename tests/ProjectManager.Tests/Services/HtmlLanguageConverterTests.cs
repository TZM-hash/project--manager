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
            <main aria-label="系統設定" data-confirm-message="確定刪除專案嗎？">
                <h1>系統設定</h1>
                <input value="專案原文" placeholder="請輸入專案名稱">
                <textarea>專案原文</textarea>
                <script>const label = "系統設定";</script>
            </main>
            """;

        var result = converter.ToSimplified(html);

        result.Should().Contain("aria-label=\"系统设置\"");
        result.Should().Contain("data-confirm-message=\"确定删除项目吗？\"");
        result.Should().Contain("<h1>系统设置</h1>");
        result.Should().Contain("value=\"專案原文\"");
        result.Should().Contain("placeholder=\"请输入项目名称\"");
        result.Should().Contain("<textarea>專案原文</textarea>");
        result.Should().Contain("const label = \"系統設定\";");
    }

    [Fact]
    public void ToTraditional_normalizes_mixed_interface_text_without_changing_form_values()
    {
        var converter = new HtmlLanguageConverter(new OpenCcConverterService());
        const string html = """
            <main aria-label="系统设置" data-confirm-message="确定删除项目吗？">
                <h1>台塑电子专案管理系统</h1>
                <p>当前人员推进状态</p>
                <input value="项目原文" placeholder="请输入项目名称">
                <textarea>项目原文</textarea>
                <script>const label = "系统设置";</script>
            </main>
            """;

        var result = converter.ToTraditional(html);

        result.Should().Contain("aria-label=\"系統設定\"");
        result.Should().Contain("data-confirm-message=\"確定刪除專案嗎？\"");
        result.Should().Contain("<h1>台塑電子專案管理系統</h1>");
        result.Should().Contain("<p>目前人員推進狀態</p>");
        result.Should().Contain("value=\"项目原文\"");
        result.Should().Contain("placeholder=\"請輸入專案名稱\"");
        result.Should().Contain("<textarea>项目原文</textarea>");
        result.Should().Contain("const label = \"系统设置\";");
    }
}
