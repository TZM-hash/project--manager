using OpenCC.NET;

namespace ProjectManager.Web.Services;

public sealed class OpenCcConverterService
{
    private readonly OpenChineseConverter _converter = new();

    public string ToTraditional(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        return _converter.ToTraditionalFromSimplified(text);
    }

    public string ToSimplified(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        return _converter.ToSimplifiedFromTraditional(text);
    }

    public string Convert(string? text, SystemSettingsService.DisplayLanguage language)
    {
        return language == SystemSettingsService.DisplayLanguage.SimplifiedChinese
            ? ToSimplified(text)
            : ToTraditional(text);
    }
}
