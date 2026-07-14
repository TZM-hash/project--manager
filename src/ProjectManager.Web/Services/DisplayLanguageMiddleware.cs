using System.Text;

namespace ProjectManager.Web.Services;

public sealed class DisplayLanguageMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        SystemSettingsService systemSettingsService,
        HtmlLanguageConverter htmlLanguageConverter)
    {
        if (Path.HasExtension(context.Request.Path.Value))
        {
            await next(context);
            return;
        }

        var language = await systemSettingsService.GetDisplayLanguageAsync(context.RequestAborted);
        var originalBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await next(context);

            responseBuffer.Position = 0;
            if (IsHtmlResponse(context.Response.ContentType))
            {
                using var reader = new StreamReader(
                    responseBuffer,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    leaveOpen: true);
                var html = await reader.ReadToEndAsync(context.RequestAborted);
                var convertedHtml = language == SystemSettingsService.DisplayLanguage.SimplifiedChinese
                    ? htmlLanguageConverter.ToSimplified(html)
                    : htmlLanguageConverter.ToTraditional(html);
                var convertedBytes = Encoding.UTF8.GetBytes(convertedHtml);
                context.Response.ContentLength = convertedBytes.Length;
                await originalBody.WriteAsync(convertedBytes, context.RequestAborted);
            }
            else
            {
                await responseBuffer.CopyToAsync(originalBody, context.RequestAborted);
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static bool IsHtmlResponse(string? contentType)
    {
        return contentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true;
    }
}
