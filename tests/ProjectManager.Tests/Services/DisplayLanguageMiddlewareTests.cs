using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class DisplayLanguageMiddlewareTests
{
    [Fact]
    public async Task Traditional_language_passes_html_through_without_conversion()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var settings = new SystemSettingsService(db);
        await settings.SetDisplayLanguageAsync(
            SystemSettingsService.DisplayLanguage.TraditionalChinese,
            CancellationToken.None);
        var converter = new HtmlLanguageConverter(new OpenCcConverterService());
        var middleware = new DisplayLanguageMiddleware(async context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync("<p>项目原文</p>", Encoding.UTF8);
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, settings, converter);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var html = await reader.ReadToEndAsync();
        html.Should().Be("<p>项目原文</p>");
    }
}
